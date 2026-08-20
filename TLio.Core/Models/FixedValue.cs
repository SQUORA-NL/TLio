using System.Globalization;
using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// An IFunctionSupportedValue that always returns a pre-set node — the simplest
/// possible "value provider" for use in command arguments.
///
/// When the parser creates a FixedValue it also hands over the original script text
/// (e.g. <c>' '</c>, <c>$.a</c>, <c>42</c>) so <see cref="ToScript"/> can reproduce the
/// expression exactly. Instances built programmatically have no script text and fall
/// back to formatting the node itself.
/// </summary>
public class FixedValue<TNode> : IFunctionSupportedValue<TNode>
{
    private readonly TNode _value;
    private readonly string? _scriptText;

    public FixedValue(TNode value) : this(value, null) { }

    /// <param name="value">The node returned by <see cref="GetValue"/>.</param>
    /// <param name="scriptText">
    /// The original script expression this value was parsed from, used verbatim by
    /// <see cref="ToScript"/>. Pass null when the value is constructed programmatically.
    /// </param>
    public FixedValue(TNode value, string? scriptText)
    {
        _value = value;
        _scriptText = scriptText;
    }

    public FunctionResult<TNode> GetValue(TNode currentNode, TNode dataContext, IExecutionContext<TNode> context) =>
        FunctionResult<TNode>.Successful(_value);

    public string ToScript() => _scriptText ?? FormatValue();

    /// <summary>
    /// Best-effort script rendering for programmatically constructed values, which carry no
    /// original text. Numbers and booleans render bare; everything else is single-quoted (with
    /// '' escaping) so it survives a re-parse as a literal argument.
    ///
    /// A value is not asked whether it looks like a path. It used to be — a string starting "$"
    /// or "@" rendered bare so it would re-parse as a path — but this class holds a node, not a
    /// path, and has no fetcher to ask what a path looks like. It was guessing one language's
    /// notation on behalf of every other. A value that is a path is a
    /// <see cref="PathValue{TNode}"/>, which knows it is one and renders itself accordingly.
    /// </summary>
    private string FormatValue()
    {
        var text = _value?.ToString();
        if (string.IsNullOrEmpty(text)) return "''";

        if (bool.TryParse(text, out _)) return text;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return text;

        return $"'{text.Replace("'", "''")}'";
    }
}
