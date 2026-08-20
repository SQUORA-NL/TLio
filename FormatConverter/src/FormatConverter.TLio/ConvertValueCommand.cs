using FormatConverter.Core;
using FormatConverter.Core.Exceptions;
using TLio.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace FormatConverter.TLio;

/// <summary>
/// Converts a value in place, without changing the format of the document around it.
/// Command name: <c>"convertValue"</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the everyday half of format conversion: a JSON envelope carrying an XML payload as a
/// string, or a document that has to hand one branch to a system speaking something else. Unlike
/// <c>convert</c>, which is a boundary in a multi-format pipeline and needs
/// <see cref="MultiFormatScriptRunner"/> to split the script, this is an ordinary
/// <see cref="ICommand{TNode}"/> and runs on the engine like any other.
/// </para>
/// <para>Two directions, chosen by whether <see cref="From"/> is given:</para>
/// <code>
/// // the node holds XML text; parse it and graft it in as structure
/// { "command": "convertValue", "path": "$.payload", "from": "xml", "to": "json" }
///
/// // the node is structure; serialise it to XML text in place
/// { "command": "convertValue", "path": "$.order", "to": "xml" }
/// </code>
/// <para>
/// The result becomes a subtree when <see cref="To"/> names the document's own format, and a
/// string otherwise — which is the only honest answer, since a JSON document has no way to hold
/// XML except as text.
/// </para>
/// <para>
/// It is a separate command name rather than <c>convert</c> with a <c>path</c> on purpose. The
/// runner intercepts <c>convert</c> before the engine ever sees it, so one name would mean a
/// mistyped <c>path</c> silently converting the whole document instead of one node.
/// </para>
/// </remarks>
/// <typeparam name="TNode">Native node type of the document's own format.</typeparam>
public sealed class ConvertValue<TNode> : CommandBase<TNode>
{
    private readonly Core.FormatConverter _converter;
    private readonly string _documentFormatId;

    /// <inheritdoc/>
    public override string CommandName => "convertValue";

    /// <summary>Path to the value(s) to convert.</summary>
    public string? Path { get; set; }

    /// <summary>
    /// Format of the source. Omitted means the node is structure in the document's own format,
    /// and is serialised rather than parsed.
    /// </summary>
    public string? From { get; set; }

    /// <summary>Format to convert to.</summary>
    public string? To { get; set; }

    /// <summary>Adapter options for this conversion.</summary>
    public ConversionSettings Settings { get; set; } = ConversionSettings.Empty;

    /// <summary>
    /// Initialises the command against a converter and the format the document itself is in.
    /// Both are known by the host at registration time — see
    /// <see cref="RegisterFormatConversionPack.RegisterFormatConversion{TNode}"/>.
    /// </summary>
    public ConvertValue(Core.FormatConverter converter, string documentFormatId)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _documentFormatId = documentFormatId ?? throw new ArgumentNullException(nameof(documentFormatId));
    }

    /// <inheritdoc/>
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

        var targets = context.ItemsFetcher.SelectNodes(Path!, dataContext).ToList();
        if (targets.Count == 0)
        {
            context.LogWarning(CoreConstants.CommandExecution,
                $"{CommandName}: no nodes matched path '{Path}' — nothing converted");
            context.TraceCollector?.Record(new TraceEntry(
                CommandName, Path!, TraceOutcome.NoOp, 0,
                $"{CommandName}: path '{Path}' matched 0 nodes; nothing converted. Verify the path is correct."));
            return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
        }

        var converted = 0;
        foreach (var target in targets)
        {
            if (TryConvert(target, context))
                converted++;
        }

        var outcome = converted == 0 ? TraceOutcome.NoOp : TraceOutcome.Success;
        context.TraceCollector?.Record(new TraceEntry(
            CommandName, Path!, outcome, converted,
            $"{CommandName}: converted {converted} of {targets.Count} node(s) at '{Path}' to {To}."));

        return new TLioExecutionResult<TNode>(IsSuccessful, dataContext);
    }

    private bool TryConvert(TNode target, IExecutionContext<TNode> context)
    {
        var adapter = context.NodeAdapter;
        var sourceFormat = From ?? _documentFormatId;

        // With `from`, the node holds text in that format. Without it, the node is structure in
        // the document's own format and has to be serialised before it can be converted.
        string source;
        if (From is null)
        {
            source = SerializeInPlace(target, adapter);
        }
        else
        {
            var text = adapter.TryGetString(target);
            if (text is null)
            {
                context.LogWarning(CoreConstants.CommandExecution,
                    $"{CommandName}: the node at '{context.ItemsFetcher.GetPath(target)}' holds no text to read as {From}");
                MarkFailed();
                return false;
            }
            source = text;
        }

        string result;
        try
        {
            result = _converter.Convert(sourceFormat, source, To!, Settings);
        }
        catch (Exception ex) when (ex is FormatParseException or FormatNotRegisteredException)
        {
            context.LogError(CoreConstants.CommandExecution,
                $"{CommandName}: could not convert '{context.ItemsFetcher.GetPath(target)}' " +
                $"from {sourceFormat} to {To}: {ex.Message}");
            MarkFailed();
            return false;
        }

        // Structure when the target format is the document's own; text otherwise, because a JSON
        // document has no way to hold XML except as a string.
        var replacement = string.Equals(To, _documentFormatId, StringComparison.OrdinalIgnoreCase)
            ? adapter.Parse(result)
            : adapter.CreateString(result);

        adapter.Replace(target, replacement);
        context.LogInfo(CoreConstants.CommandExecution,
            $"{CommandName}: converted '{context.ItemsFetcher.GetPath(replacement)}' from {sourceFormat} to {To}");
        return true;
    }

    /// <summary>
    /// Serialise a subtree along with the name it is known by, so <c>$.order</c> converts to
    /// <c>&lt;order&gt;…&lt;/order&gt;</c> rather than to its contents with the name dropped.
    /// An array element has no name to keep, and is serialised on its own.
    /// </summary>
    private static string SerializeInPlace(TNode target, INodeAdapter<TNode> adapter)
    {
        var propertyName = adapter.GetParentPropertyName(target);
        if (propertyName is null)
            return adapter.Serialize(target);

        var wrapper = adapter.CreateObject();
        adapter.SetProperty(wrapper, propertyName, adapter.DeepClone(target));
        return adapter.Serialize(wrapper);
    }

    /// <inheritdoc/>
    public override ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(Path))
            result.AddError($"{CommandName} requires a non-empty 'path'.");
        if (string.IsNullOrWhiteSpace(To))
            result.AddError($"{CommandName} requires a non-empty 'to' format.");
        return result;
    }
}
