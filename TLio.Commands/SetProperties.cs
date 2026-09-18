using TLio.Commands.Logic;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Commands;

/// <summary>
/// Runs Value against a set of nodes picked by Properties, replacing each one with the
/// result — set, but keyed by an explicit selection under Path instead of by a single path
/// expression matched the ordinary way.
///
/// This exists because TLio's write-path leaf classification does not resolve an object-key
/// wildcard (<c>$.obj.*</c>) or a multi-key union (<c>$.obj['a','b']</c>) — both read fine
/// through a plain path lookup, but <c>set</c>/<c>add</c>/<c>put</c> only recognise a leaf as a
/// multi-node selector when it is bracket-and-subscript shaped, which those two forms are not.
/// Rather than teach the path language a new selector, this command lets <see cref="Properties"/>
/// name the targets a different way, then reuses the ordinary per-node write.
///
/// <see cref="Properties"/> accepts two shapes, both evaluated once per matched object with that
/// object as the current node:
///   • an array of strings — each one either a bare property name ("tags") or an <c>@.</c>
///     relative path ("@.address.city"), resolved the same way <c>scriptpath(@.child)</c>
///     already does, so a name can reach a nested sub-item, not just a direct child;
///   • a set of live nodes — what <c>=scriptpath(*, kinds, recursive)</c> returns (see
///     <see cref="TLio.Functions.ScriptPath{TNode}"/>): each node is the write target directly,
///     no further name lookup.
/// Omitted or empty means every direct property of the object.
///
/// Deliberately generic rather than array-specific: <see cref="Value"/> is any function, the
/// same as every other command's value. Wrapping into an array is one particular value —
/// <c>=toArray()</c>, unmodified — not something this command knows about. <c>currentNode</c>
/// for each evaluation is the target's own current value, so <c>=toArray()</c> called bare
/// wraps that value; <c>=promote()</c> would nest it in an object instead; a literal replaces
/// it outright.
///
///   {"command": "setProperties", "path": "$.person", "properties": ["tags","roles"], "value": "=toArray()"}
///   {"command": "setProperties", "path": "$.person", "properties": "=scriptpath(*,['primitive'],true)", "value": "=toArray()"}
///   {"command": "setProperties", "path": "$.items[*]", "value": "=toArray()"}   // every property, every match
/// </summary>
public class SetProperties<TNode> : CommandBase<TNode>
{
    public override string CommandName => "setProperties";

    public string? Path { get; set; }

    /// <summary>
    /// Selects which nodes under each matched object to touch — a literal array of names/relative
    /// paths, or a function (typically <c>=scriptpath(*, kinds, recursive)</c>) that resolves to
    /// either shape. Unset means every direct property.
    /// </summary>
    public IFunctionSupportedValue<TNode>? Properties { get; set; }

    public IFunctionSupportedValue<TNode>? Value { get; set; }

    public SetProperties() { }

    public SetProperties(string path, IFunctionSupportedValue<TNode> value, IFunctionSupportedValue<TNode>? properties = null)
    {
        Path = path;
        Value = value;
        Properties = properties;
    }

    public override TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        ResetSuccess();
        var validation = ValidateCommandInstance();
        if (!validation.IsValid)
        {
            validation.ValidationMessages.ForEach(m => context.LogWarning(CoreConstants.CommandExecution, m));
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.Failure, 0,
                $"{CommandName}: validation failed — {string.Join("; ", validation.ValidationMessages)}."));
            return TLioExecutionResult<TNode>.Failed(dataContext);
        }

        var resolvedPath = IndirectPath.TryResolve(Path!, dataContext, context, CommandName);
        if (resolvedPath == null)
        {
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: the =indirect() expression in path '{Path}' could not be resolved; nothing changed."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        var targets = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext).ToList();
        if (targets.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: no nodes matched path '{Path}' — nothing changed");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path ?? "", TraceOutcome.NoOp, 0,
                $"{CommandName}: path '{Path}' matched 0 nodes; nothing changed."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        var adapter = context.NodeAdapter;
        var changed = 0;

        foreach (var target in targets)
        {
            if (!adapter.IsObject(target))
            {
                context.LogWarning(CoreConstants.CommandExecution,
                    $"{CommandName}: node at '{context.ItemsFetcher.GetPath(target)}' is not an object — skipped");
                continue;
            }

            foreach (var selected in ResolveSelection(target, dataContext, adapter, context))
                changed += Apply(target, selected, dataContext, adapter, context);
        }

        var outcome = changed == 0 ? TraceOutcome.NoOp : TraceOutcome.Success;
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path ?? "", outcome, changed,
            outcome == TraceOutcome.NoOp
                ? $"{CommandName}: matched {targets.Count} node(s) at '{Path}' but changed none."
                : $"{CommandName}: changed {changed} propert{(changed == 1 ? "y" : "ies")} across {targets.Count} node(s) at '{Path}'."));

        return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
    }

    /// <summary>
    /// One selected write target: either a live node already resolved (Node set, Name null — no
    /// further lookup needed) or a name/relative path still to resolve against the target object.
    /// </summary>
    private readonly record struct Selection(TNode? Node, string? Name);

    /// <summary>
    /// Turns Properties into the concrete set of things to touch on <paramref name="target"/>.
    /// No Properties, or an empty result, means every direct property — the same default either
    /// way, so a literal <c>[]</c> and simply omitting the field behave identically.
    /// </summary>
    private IEnumerable<Selection> ResolveSelection(
        TNode target, TNode dataContext, INodeAdapter<TNode> adapter, IExecutionContext<TNode> context)
    {
        if (Properties is null)
            return adapter.GetPropertyNames(target).ToList().Select(n => new Selection(default, n));

        var result = Properties.GetValue(target, dataContext, context);
        if (!result.Success || result.Data.Count == 0)
            return adapter.GetPropertyNames(target).ToList().Select(n => new Selection(default, n));

        // The two shapes are told apart by how Properties resolved as a whole, never by sniffing
        // an individual element — a live node picked by scriptpath's find mode can itself be a
        // string value ("Ada"), which is indistinguishable from a name string ("tags") once you
        // are looking at one element in isolation.
        //
        // A literal JSON/XML/YAML array value evaluates to ONE node — the array itself — so that
        // is the "list of names/relative paths" shape; the same flattening
        // CollectionFunctionBase.TryResolveElements applies for the same reason ("the path named
        // the array" vs "the path named its members"). Anything else — several nodes, or a single
        // node that is not an array — is already the live targets themselves, exactly what
        // scriptpath's find mode returns, used directly with no name lookup at all.
        if (result.Data.Count == 1 && adapter.IsArray(result.Data[0]))
        {
            return adapter.GetArrayElements(result.Data[0])
                .Select(n => adapter.TryGetString(n))
                .Where(name => name != null)
                .Select(name => new Selection(default, name!))
                .ToList();
        }

        return result.Data.Select(n => new Selection(n, null)).ToList();
    }

    /// <summary>Returns 1 if the selection was updated, 0 if it was skipped or the value failed.</summary>
    private int Apply(
        TNode target, Selection selection, TNode dataContext, INodeAdapter<TNode> adapter, IExecutionContext<TNode> context)
    {
        if (selection.Name is { } name)
            return ApplyToName(target, name, dataContext, adapter, context);

        // Already a live node (e.g. from scriptpath's find mode) — write straight into it.
        var valueResult = Value!.GetValue(selection.Node!, dataContext, context);
        if (!valueResult.Success)
        {
            MarkFailed();
            return 0;
        }
        adapter.Replace(selection.Node!, valueResult.Data.First ?? adapter.CreateNull());
        return 1;
    }

    private int ApplyToName(
        TNode target, string name, TNode dataContext, INodeAdapter<TNode> adapter, IExecutionContext<TNode> context)
    {
        TNode? existing;
        if (name.StartsWith(context.ItemsFetcher.CurrentItemPathIndicator, StringComparison.Ordinal))
        {
            var resolvedPath = context.ItemsFetcher.ResolveRelativePath(name, target, dataContext);
            var nodes = context.ItemsFetcher.SelectNodes(resolvedPath, dataContext);
            if (nodes.Count == 0)
            {
                context.LogWarning(CoreConstants.CommandExecution,
                    $"{CommandName}: relative path '{name}' matched nothing under '{context.ItemsFetcher.GetPath(target)}' — skipped");
                return 0;
            }

            var applied = 0;
            foreach (var node in nodes)
            {
                var valueResult = Value!.GetValue(node, dataContext, context);
                if (!valueResult.Success) { MarkFailed(); continue; }
                adapter.Replace(node, valueResult.Data.First ?? adapter.CreateNull());
                applied++;
            }
            return applied;
        }

        if (!adapter.HasProperty(target, name))
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: property '{name}' not found on '{context.ItemsFetcher.GetPath(target)}' — skipped");
            return 0;
        }

        // HasProperty true but GetProperty null happens for a JSON-null-valued property under
        // System.Text.Json (see PropertyChangeCommand.ReplaceProperty) — treat it as an explicit
        // null current node rather than a missing property.
        existing = adapter.GetProperty(target, name);
        var currentNode = existing ?? adapter.CreateNull();

        var result = Value!.GetValue(currentNode, dataContext, context);
        if (!result.Success)
        {
            MarkFailed();
            return 0;
        }

        var computed = result.Data.First ?? adapter.CreateNull();
        if (existing is null)
            adapter.SetProperty(target, name, computed);
        else
            adapter.Replace(existing, computed);

        return 1;
    }

    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"Path property for {CommandName} command is missing");
        if (Value is null)
            result.AddError($"Value property for {CommandName} command is missing");
        return result;
    }
}
