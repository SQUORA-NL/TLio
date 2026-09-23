using System.Globalization;
using System.Text;

namespace TLio.Extensions.ETL.Commands;

/// <summary>
/// Resolves references: for each node at Path, finds matching entries in a
/// reference collection (keyed by ResolveKeys) and writes derived values back
/// to the target node.
///
/// KeyPath / ReferenceKeyPath support:
///   "@.property"          — property relative to the current token
///   "@.array[*]"          — all elements of a relative array
///   "$.absolute.path"     — absolute JSONPath (via ItemsFetcher)
///
/// TargetPath in ResolveValue must use "@.property" notation.
///
/// Ported from JLio.Extensions.ETL.Commands.Resolve. Format-neutral via INodeAdapter.
/// </summary>
public class Resolve<TNode> : CommandBase<TNode>
{
    public override string CommandName => "resolve";

    public string Path { get; set; } = string.Empty;
    public List<ResolveSetting<TNode>> ResolveSettings { get; set; } = new();

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path, TraceOutcome.Failure, 0,
                $"resolve: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        bool foundErrors = false;
        var resolveTargets = context.ItemsFetcher.SelectNodes(Path, dataContext).ToList();

        // The reference collection and its join index do not depend on which target is being
        // resolved — build each setting's index once, shared by every target, instead of
        // re-selecting and re-scanning the reference collection per target.
        var settingIndexes = resolveTargets.Count == 0
            ? new List<ReferenceIndex>()
            : ResolveSettings.Select(setting => BuildIndex(setting, dataContext, context)).ToList();

        foreach (var target in resolveTargets)
        {
            for (var i = 0; i < ResolveSettings.Count; i++)
            {
                var setting = ResolveSettings[i];
                try
                {
                    ExecuteSetting(target, dataContext, setting, settingIndexes[i], context, ref foundErrors);
                }
                catch (Exception ex)
                {
                    context.LogWarning(CoreConstants.CommandExecution,
                        $"resolve: error in setting for '{setting.ReferencesCollectionPath}': {ex.Message}");
                }
            }
        }

        context.LogInfo(CoreConstants.CommandExecution, $"resolve: completed for {Path}");
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path,
            foundErrors ? TraceOutcome.Failure : (resolveTargets.Count == 0 ? TraceOutcome.NoOp : TraceOutcome.Success),
            resolveTargets.Count,
            foundErrors
                ? $"resolve: completed with errors for '{Path}'."
                : resolveTargets.Count == 0
                ? $"resolve: path '{Path}' matched 0 nodes; nothing resolved."
                : $"resolve: resolved {resolveTargets.Count} node(s) at '{Path}'."));
        return new TLioExecutionResult<TNode>(!foundErrors, dataContext);
    }

    private void ExecuteSetting(
        TNode target, TNode dataContext,
        ResolveSetting<TNode> setting,
        ReferenceIndex index,
        IExecutionContext<TNode> context,
        ref bool foundErrors)
    {
        var matches = FindMatches(target, index, setting.ResolveKeys, context);
        ApplyValues(target, dataContext, matches, setting.Values, setting.ResolveKeys,
                    context, ref foundErrors);
    }

    // ── Matching ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reference collection for one resolve setting, indexed once and reused for every target.
    /// <see cref="Buckets"/> holds references whose key values (all of them, for every key) are
    /// exactly one node each — the overwhelming majority of real joins — keyed by a composite,
    /// order-preserving string built from those values. <see cref="Unindexable"/> holds every
    /// reference whose key extraction did not yield exactly one value for some key (an array-
    /// based key, most commonly) — those are always checked individually, exactly as the old
    /// full scan did. A reference whose key extraction yields *zero* values for some key can
    /// never match anything (the AND-of-ORs in <see cref="IsMatch"/> is false whenever either
    /// side's value list is empty) and is dropped from both.
    /// </summary>
    private sealed class ReferenceIndex
    {
        public required List<TNode> References { get; init; }
        public required Dictionary<string, List<TNode>> Buckets { get; init; }
        public required List<TNode> Unindexable { get; init; }
    }

    private ReferenceIndex BuildIndex(
        ResolveSetting<TNode> setting, TNode dataContext, IExecutionContext<TNode> context)
    {
        var references = context.ItemsFetcher.SelectNodes(setting.ReferencesCollectionPath, dataContext).ToList();
        if (references.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"resolve: no reference collection at '{setting.ReferencesCollectionPath}'");
        }

        var buckets = new Dictionary<string, List<TNode>>(StringComparer.Ordinal);
        var unindexable = new List<TNode>();

        foreach (var reference in references)
        {
            var perKey = setting.ResolveKeys
                .Select(k => GetValues(reference, k.ReferenceKeyPath, context))
                .ToList();

            if (perKey.Any(v => v.Count == 0))
                continue; // can never match any target under this key — drop it entirely

            if (perKey.Any(v => v.Count > 1))
            {
                unindexable.Add(reference);
                continue;
            }

            var bucketKey = ComposeKey(perKey.Select(v => v[0]), context.NodeAdapter);
            if (!buckets.TryGetValue(bucketKey, out var list))
                buckets[bucketKey] = list = new List<TNode>();
            list.Add(reference);
        }

        return new ReferenceIndex { References = references, Buckets = buckets, Unindexable = unindexable };
    }

    private List<TNode> FindMatches(
        TNode target, ReferenceIndex index,
        List<ResolveKey> keys, IExecutionContext<TNode> context)
    {
        var perKey = keys.Select(k => GetValues(target, k.KeyPath, context)).ToList();

        if (perKey.Any(v => v.Count == 0))
            return new List<TNode>(); // this key can never be satisfied — nothing to scan for

        if (perKey.Any(v => v.Count > 1))
            return ScanAllReferences(target, index.References, keys, context);

        var bucketKey = ComposeKey(perKey.Select(v => v[0]), context.NodeAdapter);

        var candidates = new List<TNode>();
        if (index.Buckets.TryGetValue(bucketKey, out var bucketList))
            candidates.AddRange(bucketList);
        candidates.AddRange(index.Unindexable);

        var result = new List<TNode>();
        foreach (var candidate in candidates)
            if (IsMatch(target, candidate, keys, context))
                result.Add(candidate);
        return result;
    }

    /// <summary>Exact linear scan — the fallback for array-based (multi-valued) key extraction.</summary>
    private List<TNode> ScanAllReferences(
        TNode target, List<TNode> references,
        List<ResolveKey> keys, IExecutionContext<TNode> context)
    {
        var result = new List<TNode>();
        foreach (var reference in references)
            if (IsMatch(target, reference, keys, context))
                result.Add(reference);
        return result;
    }

    /// <summary>
    /// A hashable, order-preserving key built from a scalar value per join key. It is a
    /// performance heuristic only, never the source of truth for equality: <see cref="IsMatch"/>
    /// (via <c>DeepEquals</c>) still re-verifies every candidate a bucket lookup returns, so a
    /// coarse or colliding key can only cost speed, never correctness.
    /// </summary>
    private static string ComposeKey(IEnumerable<TNode> values, INodeAdapter<TNode> adapter)
    {
        var sb = new StringBuilder();
        foreach (var value in values)
        {
            sb.Append(NormalizeForBucket(value, adapter));
            sb.Append('\u0001');
        }
        return sb.ToString();
    }

    private static string NormalizeForBucket(TNode node, INodeAdapter<TNode> adapter)
    {
        if (adapter.IsNull(node)) return "\u0002null";

        var s = adapter.TryGetString(node);
        if (s != null) return "s:" + s;

        var d = adapter.TryGetDouble(node);
        if (d.HasValue) return "n:" + d.Value.ToString("R", CultureInfo.InvariantCulture);

        var b = adapter.TryGetBoolean(node);
        if (b.HasValue) return "b:" + b.Value;

        // Object/array key values are not expected in practice; they all collide into one
        // bucket, which — like every bucket — is still exact-matched by IsMatch afterwards.
        return "\u0003complex";
    }

    private bool IsMatch(
        TNode target, TNode reference,
        List<ResolveKey> keys, IExecutionContext<TNode> context)
    {
        foreach (var key in keys)
        {
            var targetValues    = GetValues(target,    key.KeyPath,          context);
            var referenceValues = GetValues(reference, key.ReferenceKeyPath, context);
            if (!targetValues.Any(tv => referenceValues.Any(rv =>
                    context.NodeAdapter.DeepEquals(tv, rv))))
                return false;
        }
        return true;
    }

    private List<TNode> GetValues(TNode token, string path, IExecutionContext<TNode> context)
    {
        if (path.StartsWith("@."))
        {
            var relative = path.Substring(2);
            if (path.EndsWith("[*]"))
            {
                var arrayPath = relative.Substring(0, relative.Length - 3);
                var arrayNode = GetNestedProperty(token, arrayPath, context.NodeAdapter);
                if (arrayNode != null && context.NodeAdapter.IsArray(arrayNode))
                    return context.NodeAdapter.GetArrayElements(arrayNode).ToList();
            }
            else
            {
                var val = GetNestedProperty(token, relative, context.NodeAdapter);
                if (val != null) return new List<TNode> { val };
            }
            return new List<TNode>();
        }
        return context.ItemsFetcher.SelectNodes(path, token).ToList();
    }

    private static TNode? GetNestedProperty(TNode node, string path, INodeAdapter<TNode> adapter)
    {
        var current = node;
        foreach (var part in path.Split('.'))
        {
            if (!adapter.IsObject(current)) return default;
            var next = adapter.GetProperty(current, part);
            if (next == null) return default;
            current = next;
        }
        return current;
    }

    // ── Value application ──────────────────────────────────────────────────────

    private void ApplyValues(
        TNode target, TNode dataContext,
        List<TNode> matches, List<ResolveValue<TNode>> values,
        List<ResolveKey> keys, IExecutionContext<TNode> context,
        ref bool foundErrors)
    {
        foreach (var resolveValue in values)
        {
            if (resolveValue.Value == null) continue;
            try
            {
                if (resolveValue.TargetPathExpression != null)
                {
                    ApplyDynamicTargetValue(target, dataContext, matches, resolveValue, context);
                    continue;
                }

                var toAssign = BuildResult(matches, dataContext, resolveValue, keys, context);
                if (toAssign != null)
                    SetValueAtPath(target, resolveValue.TargetPath, toAssign, context);
            }
            catch (Exception ex)
            {
                foundErrors = true;
                context.LogWarning(CoreConstants.CommandExecution,
                    $"resolve: error applying value to '{resolveValue.TargetPath}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Writes a value under a property name computed from the matched reference entry, for a
    /// <see cref="ResolveValue{TNode}.TargetPathExpression"/>. Requires exactly one match — with
    /// zero there is nothing to name the property after, and with more than one there is no
    /// defined meaning for writing several differently-named properties from one
    /// <c>targetPath</c> expression — either case is a warning and a skip, never a throw.
    /// </summary>
    private void ApplyDynamicTargetValue(
        TNode target, TNode dataContext, List<TNode> matches,
        ResolveValue<TNode> resolveValue, IExecutionContext<TNode> context)
    {
        if (matches.Count != 1)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"resolve: dynamic targetPath '{resolveValue.TargetPath}' requires exactly one match, found {matches.Count} — value not written.");
            return;
        }

        if (!context.NodeAdapter.IsObject(target))
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"resolve: dynamic targetPath '{resolveValue.TargetPath}' requires an object target — value not written.");
            return;
        }

        var matchRef = matches[0];
        var nameResult = resolveValue.TargetPathExpression!.GetValue(matchRef, dataContext, context);
        var propertyName = nameResult.Success && nameResult.Data.First != null
            ? context.NodeAdapter.TryGetString(nameResult.Data.First)
            : null;

        if (string.IsNullOrEmpty(propertyName))
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"resolve: dynamic targetPath '{resolveValue.TargetPath}' did not resolve to a usable property name — value not written.");
            return;
        }

        var value = GetResolvedValue(matchRef, dataContext, resolveValue.Value!, context);
        if (value != null)
            context.NodeAdapter.SetProperty(target, propertyName, value);
    }

    private TNode? BuildResult(
        List<TNode> matches, TNode dataContext,
        ResolveValue<TNode> resolveValue,
        List<ResolveKey> keys, IExecutionContext<TNode> context)
    {
        switch (resolveValue.ResolveTypeBehavior)
        {
            case ResolveTypeBehavior.AlwaysAsArray:
                return BuildArray(matches, dataContext, resolveValue.Value!, context);

            case ResolveTypeBehavior.AlwaysAsObject:
                if (matches.Count == 0) return default;
                if (matches.Count > 1)
                    throw new InvalidOperationException(
                        $"Multiple matches ({matches.Count}) but ResolveTypeBehavior is AlwaysAsObject");
                return GetResolvedValue(matches[0], dataContext, resolveValue.Value!, context);

            default: // DependingOnResult
                bool arrayBased = keys.Any(k => k.KeyPath.EndsWith("[*]") && k.ReferenceKeyPath.EndsWith("[*]"));
                if (arrayBased)
                    return BuildArray(matches, dataContext, resolveValue.Value!, context);
                if (matches.Count == 1)
                    return GetResolvedValue(matches[0], dataContext, resolveValue.Value!, context);
                if (matches.Count > 1)
                    return BuildArray(matches, dataContext, resolveValue.Value!, context);
                return default;
        }
    }

    private TNode BuildArray(
        List<TNode> matches, TNode dataContext,
        IFunctionSupportedValue<TNode> valueTemplate,
        IExecutionContext<TNode> context)
    {
        var arr = context.NodeAdapter.CreateArray();
        foreach (var m in matches)
        {
            var v = GetResolvedValue(m, dataContext, valueTemplate, context);
            if (v != null) context.NodeAdapter.AppendToArray(arr, v);
        }
        return arr;
    }

    private static TNode? GetResolvedValue(
        TNode matchRef, TNode dataContext,
        IFunctionSupportedValue<TNode> valueTemplate,
        IExecutionContext<TNode> context)
    {
        if (TryReadFromMatch(matchRef, valueTemplate, context, out var fromMatch))
            return fromMatch;

        var result = valueTemplate.GetValue(matchRef, dataContext, context);
        if (!result.Success || result.Data.Count == 0) return context.NodeAdapter.CreateNull();
        return result.Data.First;
    }

    /// <summary>
    /// Read <c>"value": "@.field"</c> off the matched reference entry with the adapter, the same
    /// way <see cref="KeysMatch"/> reads <c>keyPath</c> and <see cref="SetValueAtPath"/> writes
    /// <c>targetPath</c>.
    ///
    /// It cannot go through the path language, because the matched entry is not something a path
    /// can name. <c>referencesCollectionPath</c> matched several nodes and this is one of them;
    /// in XML every one of them has the same absolute path, so resolving the relative path to an
    /// absolute one and selecting it again returns the *first* sibling rather than the match. The
    /// nesting separator is the notation's <c>.</c> for the same reason — <c>@.detail.tier</c>
    /// is a walk over properties, not a path in the document's language.
    ///
    /// So <c>@.</c> means one thing in a resolve setting, in every format. A value that is not
    /// written that way — a literal, a function expression, an absolute path — is left to the
    /// ordinary value machinery.
    /// </summary>
    private static bool TryReadFromMatch(
        TNode matchRef, IFunctionSupportedValue<TNode> valueTemplate,
        IExecutionContext<TNode> context, out TNode? value)
    {
        value = default;

        if (valueTemplate is not PathValue<TNode>) return false;

        var path = valueTemplate.ToScript();
        if (!path.StartsWith("@.", StringComparison.Ordinal)) return false;

        value = GetNestedProperty(matchRef, path[2..], context.NodeAdapter)
                ?? context.NodeAdapter.CreateNull();
        return true;
    }

    private static void SetValueAtPath(
        TNode target, string path, TNode value, IExecutionContext<TNode> context)
    {
        if (path.StartsWith("@.") && context.NodeAdapter.IsObject(target))
        {
            var parts = path.Substring(2).Split('.');
            var current = target;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!context.NodeAdapter.HasProperty(current, parts[i]))
                    context.NodeAdapter.SetProperty(current, parts[i], context.NodeAdapter.CreateObject());
                current = context.NodeAdapter.GetProperty(current, parts[i])!;
            }
            context.NodeAdapter.SetProperty(current, parts[^1], value);
        }
        else
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"resolve: unsupported target path format '{path}' (use @.property notation)");
        }
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.ValidationMessages.Add($"{CommandName}: Path property is required");
        if (ResolveSettings == null || !ResolveSettings.Any())
            result.ValidationMessages.Add($"{CommandName}: ResolveSettings are required");
        else
            foreach (var s in ResolveSettings)
            {
                if (s.ResolveKeys == null || !s.ResolveKeys.Any())
                    result.ValidationMessages.Add("ResolveKeys are required for each resolve setting");
                if (string.IsNullOrWhiteSpace(s.ReferencesCollectionPath))
                    result.ValidationMessages.Add("ReferencesCollectionPath is required for each resolve setting");
                if (s.Values == null || !s.Values.Any())
                    result.ValidationMessages.Add("Values are required for each resolve setting");
            }
        return result;
    }
}
