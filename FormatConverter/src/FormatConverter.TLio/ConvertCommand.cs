using System.Text.Json;
using FormatConverter.Core;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace FormatConverter.TLio;

/// <summary>
/// TLio command that signals a format boundary in a multi-format script.
/// Command name: <c>"convert"</c>.
/// </summary>
/// <remarks>
/// This command is intercepted by <see cref="MultiFormatScriptRunner"/> during pre-processing.
/// If executed directly by the standard TLio engine (outside a multi-format runner),
/// it is a no-op and logs a warning.
/// </remarks>
/// <typeparam name="TNode">Native node type of the current section's format.</typeparam>
public sealed class ConvertCommand<TNode> : ICommand<TNode>
{
    /// <inheritdoc/>
    public string CommandName => "convert";

    /// <summary>Target format ID parsed from the script.</summary>
    public string To { get; private set; } = string.Empty;

    /// <summary>Per-boundary settings parsed from the script.</summary>
    public ConversionSettings Settings { get; private set; } = ConversionSettings.Empty;

    /// <summary>Initialises a new instance (used by command factory).</summary>
    public ConvertCommand() { }

    /// <summary>Initialises with pre-parsed values (used in tests).</summary>
    public ConvertCommand(string to, ConversionSettings settings)
    {
        To = to;
        Settings = settings;
    }

    /// <summary>
    /// Parses a <see cref="ConvertCommand{TNode}"/> from the raw JSON command element.
    /// </summary>
    public static ConvertCommand<TNode> Parse(JsonElement element)
    {
        var to = element.TryGetProperty("to", out var toProp) ? toProp.GetString() ?? string.Empty : string.Empty;
        return new ConvertCommand<TNode>(to, ConvertSettingsReader.Read(element));
    }

    /// <summary>
    /// Outside <see cref="MultiFormatScriptRunner"/> this cannot do anything, and reports failure
    /// rather than pretending otherwise.
    /// </summary>
    public TLioExecutionResult<TNode> Execute(TNode dataContext, IExecutionContext<TNode> context)
    {
        // Failure rather than a warning: the document is still in the format it started in, and
        // reporting success would tell the caller a conversion happened that did not.
        context.LogError("ConvertCommand",
            $"'convert' to '{To}' did nothing: it marks a format boundary, and only " +
            "MultiFormatScriptRunner can split a script at one — the engine works in a single " +
            "node type from start to finish. Run the script through the runner, or use " +
            "'convertValue' to convert one value in place.");
        return TLioExecutionResult<TNode>.Failed(dataContext);
    }

    /// <inheritdoc/>
    public ValidationResult ValidateCommandInstance()
    {
        var result = new ValidationResult();
        if (string.IsNullOrWhiteSpace(To))
            result.AddError("ConvertCommand requires a non-empty 'to' property.");
        return result;
    }

    /// <inheritdoc/>
    public ICommand<TNode> Clone() => new ConvertCommand<TNode>(To, Settings);
}
