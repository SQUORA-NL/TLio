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
        foreach (var target in resolveTargets)
        {
            foreach (var setting in ResolveSettings)
            {
                try
                {
                    ExecuteSetting(target, dataContext, setting, context, ref foundErrors);
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
        IExecutionContext<TNode> context,
        ref bool foundErrors)
    {
        var references = context.ItemsFetcher.SelectNodes(setting.ReferencesCollectionPath, dataContext).ToList();
        if (references.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"resolve: no reference collection at '{setting.ReferencesCollectionPath}'");
            return;
        }

        var matches = FindMatches(target, references, setting.ResolveKeys, context);
        ApplyValues(target, dataContext, matches, setting.Values, setting.ResolveKeys,
                    context, ref foundErrors);
    }

    // ── Matching ──────────────────────────────────────────────────────────────

    private List<TNode> FindMatches(
        TNode target, List<TNode> references,
        List<ResolveKey> keys, IExecutionContext<TNode> context)
    {
        var result = new List<TNode>();
        foreach (var reference in references)
            if (IsMatch(target, reference, keys, context))
                result.Add(reference);
        return result;
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
