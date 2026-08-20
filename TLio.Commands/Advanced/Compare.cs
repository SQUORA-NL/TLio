using System.Globalization;
using TLio.Commands.Advanced.Models;
using TLio.Commands.Advanced.Settings;
using TLio.Commands.Logic;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands.Advanced;

/// <summary>
/// Compares the node(s) at FirstPath with the node(s) at SecondPath and writes the
/// outcome to ResultPath.
///
/// Two output shapes exist:
///
///   • Scalar (backwards compatible) — written when Settings are default and both
///     sides resolve to a single primitive node:
///       "equal"     — the values are equivalent
///       "greater"   — first is numerically greater than second
///       "less"      — first is numerically less than second
///       "different" — values differ and cannot be ordered
///
///   • Structured diff — an array node whose elements describe each difference:
///       { foundDifference, differenceType, differenceSubType, firstPath, secondPath, description }
///
/// Everything is expressed through INodeAdapter / IItemsFetcher, so the same diff
/// runs unchanged over JSON, XML and YAML documents. Paths in the result use the
/// notation of the active fetcher ("$.a.b", "/a/b", …).
///
/// Ported from JLio's Compare command.
/// </summary>
public class Compare<TNode> : CommandBase<TNode>
{
    /// <summary>Guards against pathological nesting and cyclic YAML anchors.</summary>
    private const int MaxDepth = 128;

    public override string CommandName => "compare";

    public string? FirstPath { get; set; }
    public string? SecondPath { get; set; }
    public string? ResultPath { get; set; }

    /// <summary>Optional diff configuration. Null means "use defaults".</summary>
    public CompareSettings? Settings { get; set; }

    // JLio-compatible aliases (FR-001/FR-002)
    public string? FromPath { set => FirstPath = value; }
    public string? ToPath   { set => SecondPath = value; }

    public Compare() { }

    public Compare(string firstPath, string secondPath, string resultPath)
    {
        FirstPath = firstPath;
        SecondPath = secondPath;
        ResultPath = resultPath;
    }

    public Compare(string firstPath, string secondPath, string resultPath, CompareSettings settings)
        : this(firstPath, secondPath, resultPath)
    {
        Settings = settings;
    }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, $"{FirstPath} vs {SecondPath}", TraceOutcome.Failure, 0,
                $"{CommandName}: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        // A path may be an =indirect() expression; the raw '=' would throw out of the fetcher.
        var resolvedFirst = IndirectPath.TryResolve(FirstPath!, dataContext, context, CommandName, "firstPath");
        var resolvedSecond = resolvedFirst == null
            ? null
            : IndirectPath.TryResolve(SecondPath!, dataContext, context, CommandName, "secondPath");
        if (resolvedFirst == null || resolvedSecond == null)
        {
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, $"{FirstPath} vs {SecondPath}", TraceOutcome.NoOp, 0,
                $"{CommandName}: an =indirect() path expression could not be resolved; comparison skipped."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var firstNodes = context.ItemsFetcher.SelectNodes(resolvedFirst, dataContext);
        var secondNodes = context.ItemsFetcher.SelectNodes(resolvedSecond, dataContext);

        if (firstNodes.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no node at FirstPath '{FirstPath}'");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, $"{FirstPath} vs {SecondPath}", TraceOutcome.NoOp, 0,
                $"{CommandName}: FirstPath '{FirstPath}' matched 0 nodes; comparison skipped."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }
        if (secondNodes.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution, $"{CommandName}: no node at SecondPath '{SecondPath}'");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, $"{FirstPath} vs {SecondPath}", TraceOutcome.NoOp, 0,
                $"{CommandName}: SecondPath '{SecondPath}' matched 0 nodes; comparison skipped."));
            return TLioExecutionResult<TNode>.Successful(dataContext);
        }

        var settings = Settings ?? CompareSettings.CreateDefault();
        var differ = new Differ(context, settings);

        TNode resultNode;
        string traceSummary;

        if (settings.IsDefault &&
            firstNodes.Count == 1 && secondNodes.Count == 1 &&
            differ.IsScalar(firstNodes[0]) && differ.IsScalar(secondNodes[0]))
        {
            var scalar = differ.ComputeScalarResult(firstNodes[0], secondNodes[0]);
            resultNode = context.NodeAdapter.CreateString(scalar);
            traceSummary = $"result = '{scalar}'";
            context.LogInfo(CoreConstants.CommandExecution, $"{CommandName}: result = '{scalar}'");
        }
        else
        {
            var results = new CompareResults();
            foreach (var first in firstNodes)
                foreach (var second in secondNodes)
                    differ.CompareNodes(first, second, 0, results);

            var filtered = Filter(results, settings);
            resultNode = differ.ToNode(filtered);
            traceSummary = $"{filtered.Count} result(s), differences: {filtered.ContainsDifference}";
            context.LogInfo(CoreConstants.CommandExecution,
                $"{CommandName}: {filtered.Count} compare result(s); differences found: {filtered.ContainsDifference}");
        }

        WriteResult(dataContext, resultNode, context);

        context.TraceCollector?.Record(new TraceEntry(
            CommandName, $"{FirstPath} vs {SecondPath}", TraceOutcome.Success, 1,
            $"{CommandName}: compared '{FirstPath}' and '{SecondPath}'; {traceSummary} written to '{ResultPath}'."));
        return TLioExecutionResult<TNode>.Successful(dataContext);
    }

    private static CompareResults Filter(CompareResults results, CompareSettings settings)
    {
        if (settings.ResultTypes == null || settings.ResultTypes.Count == 0)
            return results;
        return new CompareResults(results.Where(r => settings.ResultTypes.Contains(r.DifferenceType)));
    }

    private void WriteResult(TNode dataContext, TNode resultNode, IExecutionContext<TNode> context)
    {
        // resultPath may be an =indirect() expression too; an unresolved one cannot be written to.
        var resolvedResultPath = IndirectPath.TryResolve(
            ResultPath!, dataContext, context, CommandName, "resultPath");
        if (resolvedResultPath == null)
            return;

        var (parentPath, leafName) = context.ItemsFetcher.SplitParentAndLeaf(resolvedResultPath);
        context.ItemsFetcher.EnsurePath(resolvedResultPath, dataContext, context.NodeAdapter);
        var parents = context.ItemsFetcher.SelectNodes(parentPath, dataContext);

        foreach (var parent in parents)
            context.NodeAdapter.SetProperty(parent, leafName, context.NodeAdapter.DeepClone(resultNode));
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(FirstPath)) result.AddError($"{CommandName}: FirstPath is required.");
        if (string.IsNullOrWhiteSpace(SecondPath)) result.AddError($"{CommandName}: SecondPath is required.");
        if (string.IsNullOrWhiteSpace(ResultPath)) result.AddError($"{CommandName}: ResultPath is required.");
        return result;
    }

    // ── Diff engine ───────────────────────────────────────────────────────────

    private enum NodeKind { Null, Primitive, Object, Array }

    /// <summary>
    /// Holds the per-execution state of a diff run. Instantiated inside Execute so
    /// the command instance itself stays free of execution state.
    /// </summary>
    private sealed class Differ
    {
        private readonly IExecutionContext<TNode> _context;
        private readonly CompareSettings _settings;
        private INodeAdapter<TNode> Adapter => _context.NodeAdapter;
        private IItemsFetcher<TNode> Fetcher => _context.ItemsFetcher;

        internal Differ(IExecutionContext<TNode> context, CompareSettings settings)
        {
            _context = context;
            _settings = settings;
        }

        // ── Public entry points ───────────────────────────────────────────────

        internal bool IsScalar(TNode node) => KindOf(node) is NodeKind.Primitive or NodeKind.Null;

        /// <summary>Backwards-compatible single-word verdict for two primitives.</summary>
        internal string ComputeScalarResult(TNode first, TNode second)
        {
            if (PrimitivesEqual(first, second)) return "equal";

            var firstNum = Adapter.TryGetDouble(first);
            var secondNum = Adapter.TryGetDouble(second);
            if (firstNum.HasValue && secondNum.HasValue)
            {
                if (firstNum.Value > secondNum.Value) return "greater";
                if (firstNum.Value < secondNum.Value) return "less";
                return "equal";
            }

            return "different";
        }

        internal void CompareNodes(TNode first, TNode second, int depth, CompareResults results)
        {
            if (depth > MaxDepth) return;

            var firstPath = Fetcher.GetPath(first);
            var secondPath = Fetcher.GetPath(second);

            var firstKind = KindOf(first);
            var secondKind = KindOf(second);

            if (firstKind != secondKind)
            {
                results.Add(CompareResult.TypeDifference(firstPath, secondPath,
                    $"The types are different. Source: ({firstPath}) --> {firstKind} - Target:({secondPath}) --> {secondKind}"));
                return;
            }

            if (StructurallyEqual(first, second, 0))
            {
                results.Add(CompareResult.Equal(firstPath, secondPath,
                    $"The values are the same. Source: ({firstPath}) - Target:({secondPath})"));
                return;
            }

            switch (firstKind)
            {
                case NodeKind.Object:
                    CompareObjects(first, second, firstPath, secondPath, depth, results);
                    break;
                case NodeKind.Array:
                    CompareArrays(first, second, firstPath, secondPath, depth, results);
                    break;
                default:
                    ComparePrimitives(first, second, firstPath, secondPath, results);
                    break;
            }
        }

        /// <summary>Serialise the diff into a node array using only adapter primitives.</summary>
        internal TNode ToNode(CompareResults results)
        {
            var array = Adapter.CreateArray();
            foreach (var r in results)
            {
                var entry = Adapter.CreateObject();
                Adapter.SetProperty(entry, "foundDifference", Adapter.CreateBoolean(r.FoundDifference));
                Adapter.SetProperty(entry, "differenceType", Adapter.CreateString(ToCamelCase(r.DifferenceType.ToString())));
                Adapter.SetProperty(entry, "differenceSubType", Adapter.CreateString(ToCamelCase(r.DifferenceSubType.ToString())));
                Adapter.SetProperty(entry, "firstPath", Adapter.CreateString(r.FirstPath ?? string.Empty));
                Adapter.SetProperty(entry, "secondPath", Adapter.CreateString(r.SecondPath ?? string.Empty));
                Adapter.SetProperty(entry, "description", Adapter.CreateString(r.Description));
                Adapter.AppendToArray(array, entry);
            }
            return array;
        }

        // ── Object / array / primitive diff ───────────────────────────────────

        private void CompareObjects(TNode first, TNode second, string firstPath, string secondPath,
            int depth, CompareResults results)
        {
            var propertyNames = Adapter.GetPropertyNames(first)
                .Concat(Adapter.GetPropertyNames(second))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            foreach (var propertyName in propertyNames)
            {
                var inFirst = Adapter.HasProperty(first, propertyName);
                var inSecond = Adapter.HasProperty(second, propertyName);

                if (!inFirst || !inSecond)
                {
                    results.Add(CompareResult.StructureDifference(
                        Combine(firstPath, propertyName), Combine(secondPath, propertyName),
                        $"The structure is different. Source: ({Combine(firstPath, propertyName)}) --> {inFirst} - " +
                        $"Target:({Combine(secondPath, propertyName)}) --> {inSecond}"));
                    continue;
                }

                var firstChild = Adapter.GetProperty(first, propertyName);
                var secondChild = Adapter.GetProperty(second, propertyName);
                if (firstChild is null || secondChild is null) continue;

                CompareNodes(firstChild, secondChild, depth + 1, results);
            }
        }

        private void CompareArrays(TNode first, TNode second, string firstPath, string secondPath,
            int depth, CompareResults results)
        {
            var firstLength = Adapter.GetArrayLength(first);
            var secondLength = Adapter.GetArrayLength(second);

            results.Add(firstLength == secondLength
                ? CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.Equals, false,
                    $"Both arrays have {firstLength} items. Source: ({firstPath}) - Target:({secondPath})")
                : CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.NotEquals, true,
                    $"The arrays have a different number of items. Source: ({firstPath}): {firstLength} - " +
                    $"Target:({secondPath}): {secondLength}"));

            var arraySettings = FindArraySettings(firstPath, secondPath);

            if (arraySettings?.KeyPaths is { Count: > 0 })
                CompareArraysByKey(first, second, firstPath, secondPath, arraySettings, depth, results);
            else
                CompareArraysByIndex(first, second, firstPath, secondPath, firstLength, secondLength, depth, results);
        }

        private void CompareArraysByIndex(TNode first, TNode second, string firstPath, string secondPath,
            int firstLength, int secondLength, int depth, CompareResults results)
        {
            var shared = Math.Min(firstLength, secondLength);
            for (var i = 0; i < shared; i++)
                CompareNodes(Adapter.GetArrayElement(first, i), Adapter.GetArrayElement(second, i), depth + 1, results);

            for (var i = shared; i < firstLength; i++)
                results.Add(CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.NotEquals, true,
                    $"The item at index {i} exists in the first array only. Source: ({firstPath}) --> True - " +
                    $"Target:({secondPath}) --> False. Value:{Describe(Adapter.GetArrayElement(first, i))}"));

            for (var i = shared; i < secondLength; i++)
                results.Add(CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.NotEquals, true,
                    $"The item at index {i} exists in the second array only. Source: ({firstPath}) --> False - " +
                    $"Target:({secondPath}) --> True. Value:{Describe(Adapter.GetArrayElement(second, i))}"));
        }

        private void CompareArraysByKey(TNode first, TNode second, string firstPath, string secondPath,
            CompareArraySettings arraySettings, int depth, CompareResults results)
        {
            var firstLength = Adapter.GetArrayLength(first);
            var secondLength = Adapter.GetArrayLength(second);
            var matchedSecondIndexes = new HashSet<int>();

            for (var i = 0; i < firstLength; i++)
            {
                var item = Adapter.GetArrayElement(first, i);
                var matchIndex = FindMatch(second, secondLength, item, matchedSecondIndexes, arraySettings.KeyPaths);

                if (matchIndex < 0)
                {
                    results.Add(CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.NotEquals, true,
                        $"The item exists in the first array only. Source: ({firstPath}) --> True - " +
                        $"Target:({secondPath}) --> False. Value:{Describe(item)}"));
                    continue;
                }

                matchedSecondIndexes.Add(matchIndex);
                var match = Adapter.GetArrayElement(second, matchIndex);

                results.Add(CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.Equals, false,
                    $"Both arrays contain a matching item. Source: ({firstPath}) - Target:({secondPath}). Value:{Describe(item)}"));

                if (arraySettings.UniqueIndexMatching && matchIndex != i)
                    results.Add(CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.IndexDifference, true,
                        $"The indexes of the matched items are different. Source: ({firstPath})[{i}] - " +
                        $"Target:({secondPath})[{matchIndex}]"));

                CompareNodes(item, match, depth + 1, results);
            }

            for (var j = 0; j < secondLength; j++)
            {
                if (matchedSecondIndexes.Contains(j)) continue;
                results.Add(CompareResult.ArrayDifference(firstPath, secondPath, DifferenceSubType.NotEquals, true,
                    $"The item exists in the second array only. Source: ({firstPath}) --> False - " +
                    $"Target:({secondPath}) --> True. Value:{Describe(Adapter.GetArrayElement(second, j))}"));
            }
        }

        private void ComparePrimitives(TNode first, TNode second, string firstPath, string secondPath,
            CompareResults results)
        {
            var firstNum = Adapter.TryGetDouble(first);
            var secondNum = Adapter.TryGetDouble(second);

            if (firstNum.HasValue && secondNum.HasValue)
            {
                var subType = firstNum.Value < secondNum.Value
                    ? DifferenceSubType.LessThan
                    : DifferenceSubType.GreaterThan;
                results.Add(CompareResult.ValueDifference(firstPath, secondPath, subType,
                    $"The values are different {subType}. Source: ({firstPath}) --> " +
                    $"{firstNum.Value.ToString(CultureInfo.InvariantCulture)} - Target:({secondPath}) --> " +
                    $"{secondNum.Value.ToString(CultureInfo.InvariantCulture)}"));
                return;
            }

            results.Add(CompareResult.ValueDifference(firstPath, secondPath, DifferenceSubType.NotEquals,
                $"The values are different. Source: ({firstPath}) --> {Describe(first)} - " +
                $"Target:({secondPath}) --> {Describe(second)}"));
        }

        // ── Array key matching ────────────────────────────────────────────────

        private int FindMatch(TNode array, int length, TNode item, HashSet<int> taken, List<string> keyPaths)
        {
            for (var i = 0; i < length; i++)
            {
                if (taken.Contains(i)) continue;
                var candidate = Adapter.GetArrayElement(array, i);
                if (AllKeysMatch(item, candidate, keyPaths)) return i;
            }
            return -1;
        }

        private bool AllKeysMatch(TNode first, TNode second, List<string> keyPaths)
        {
            foreach (var keyPath in keyPaths)
            {
                var a = ResolveKey(first, keyPath);
                var b = ResolveKey(second, keyPath);
                if (a is null && b is null) continue;
                if (a is null || b is null) return false;
                if (!StructurallyEqual(a, b, 0)) return false;
            }
            return true;
        }

        /// <summary>
        /// Resolve a relative key path against an array element. Segment walking via the
        /// adapter is tried first (identical semantics for JSON / XML / YAML); the active
        /// fetcher is the fallback for expressions the walk cannot handle.
        /// </summary>
        private TNode? ResolveKey(TNode item, string keyPath)
        {
            var segments = KeySegments(keyPath);
            if (segments.Count == 0) return item;

            var current = item;
            var walked = true;
            foreach (var segment in segments)
            {
                var next = Adapter.IsObject(current) ? Adapter.GetProperty(current, segment) : default;
                if (next is null) { walked = false; break; }
                current = next;
            }
            if (walked) return current;

            return Fetcher.SelectNodes(AbsoluteKeyPath(keyPath), item).FirstOrDefault();
        }

        private List<string> KeySegments(string keyPath)
        {
            var trimmed = StripRelativePrefixes(keyPath);
            var root = Fetcher.RootPathIndicator;
            if (trimmed.StartsWith(root, StringComparison.Ordinal))
                trimmed = trimmed.Substring(root.Length);

            return trimmed
                .Split(Fetcher.PathDelimiter, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private string AbsoluteKeyPath(string keyPath)
        {
            var root = Fetcher.RootPathIndicator;
            var delimiter = Fetcher.PathDelimiter;
            var path = StripRelativePrefixes(keyPath);

            if (path.StartsWith(root + delimiter, StringComparison.Ordinal) || path == root)
                return path;

            path = path.TrimStart(delimiter.ToCharArray());
            if (path.Length == 0) return root;

            return root.EndsWith(delimiter, StringComparison.Ordinal) ? root + path : root + delimiter + path;
        }

        /// <summary>
        /// Drops the "this node" marker from the front of a key path, so a path written
        /// relative to the item is read as a path within it.
        ///
        /// The marker is whatever the fetcher declares — this used to strip a literal "@" as
        /// well, which is that marker only in the JSONPath-shaped languages. In XPath "@" opens
        /// an attribute reference, so the same line quietly ate the "@" of "@id".
        /// </summary>
        private string StripRelativePrefixes(string keyPath)
        {
            var path = keyPath.Trim();

            var current = Fetcher.CurrentItemPathIndicator;
            if (current.Length > 0 && current != Fetcher.RootPathIndicator &&
                path.StartsWith(current, StringComparison.Ordinal))
                path = path.Substring(current.Length);

            return path;
        }

        private CompareArraySettings? FindArraySettings(string firstPath, string secondPath)
        {
            if (_settings.ArraySettings == null || _settings.ArraySettings.Count == 0) return null;
            return _settings.ArraySettings.FirstOrDefault(s =>
                string.Equals(s.ArrayPath, firstPath, StringComparison.Ordinal) ||
                string.Equals(s.ArrayPath, secondPath, StringComparison.Ordinal));
        }

        // ── Format-agnostic equality ──────────────────────────────────────────

        /// <summary>
        /// Deep equality expressed purely through the adapter, so that formats whose
        /// native equality also compares node names (XML) still report equal content
        /// under differently named elements.
        /// </summary>
        private bool StructurallyEqual(TNode first, TNode second, int depth)
        {
            if (depth > MaxDepth) return false;

            var kind = KindOf(first);
            if (kind != KindOf(second)) return false;

            switch (kind)
            {
                case NodeKind.Null:
                    return true;

                case NodeKind.Primitive:
                    return PrimitivesEqual(first, second);

                case NodeKind.Array:
                {
                    var length = Adapter.GetArrayLength(first);
                    if (length != Adapter.GetArrayLength(second)) return false;
                    for (var i = 0; i < length; i++)
                        if (!StructurallyEqual(Adapter.GetArrayElement(first, i), Adapter.GetArrayElement(second, i), depth + 1))
                            return false;
                    return true;
                }

                default:
                {
                    var firstNames = Adapter.GetPropertyNames(first).ToList();
                    var secondNames = Adapter.GetPropertyNames(second).ToList();
                    if (firstNames.Count != secondNames.Count) return false;
                    foreach (var name in firstNames)
                    {
                        if (!Adapter.HasProperty(second, name)) return false;
                        var a = Adapter.GetProperty(first, name);
                        var b = Adapter.GetProperty(second, name);
                        if (a is null || b is null) return false;
                        if (!StructurallyEqual(a, b, depth + 1)) return false;
                    }
                    return true;
                }
            }
        }

        private bool PrimitivesEqual(TNode first, TNode second)
        {
            var firstText = Adapter.TryGetString(first);
            var secondText = Adapter.TryGetString(second);
            if (string.Equals(firstText, secondText, StringComparison.Ordinal)) return true;

            var firstNum = Adapter.TryGetDouble(first);
            var secondNum = Adapter.TryGetDouble(second);
            return firstNum.HasValue && secondNum.HasValue &&
                   Math.Abs(firstNum.Value - secondNum.Value) < 0.0000000001;
        }

        private NodeKind KindOf(TNode node)
        {
            // Null before the containers: an empty XML element answers IsNull and IsObject at
            // once, and comparing it as Object put a full difference record where JSON's null
            // gets the compact scalar verdict. The adapter's own GetNodeKind resolves the same
            // tie the same way.
            if (Adapter.IsNull(node)) return NodeKind.Null;

            // Array before Object: XML models an array as an element whose children
            // all share one name, which also satisfies IsObject.
            if (Adapter.IsArray(node)) return NodeKind.Array;
            if (Adapter.IsObject(node)) return NodeKind.Object;
            return NodeKind.Primitive;
        }

        // ── Formatting helpers ────────────────────────────────────────────────

        private string Describe(TNode node)
        {
            try { return Adapter.Serialize(node); }
            catch { return Adapter.TryGetString(node) ?? string.Empty; }
        }

        private string Combine(string path, string propertyName)
        {
            var delimiter = Fetcher.PathDelimiter;
            return path.EndsWith(delimiter, StringComparison.Ordinal)
                ? path + propertyName
                : path + delimiter + propertyName;
        }

        private static string ToCamelCase(string value) =>
            string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
