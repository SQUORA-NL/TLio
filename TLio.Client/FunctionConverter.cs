using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Parses "=funcName(arg1, arg2, ...)" string expressions into IFunctionSupportedValue instances.
///
/// Value parsing rules (mirrors JLio):
///   - Starts with "==" → FixedValue with literal "=" + remainder (escape sequence)
///   - Starts with "$$" → FixedValue with literal "$" + remainder (escape sequence)
///   - Starts with "@@" → FixedValue with literal "@" + remainder (escape sequence)
///   - Starts with "=" → function call expression
///   - Starts with "$" or "@" → path expression (PathValue)
///   - Quoted string 'value' or "value" → FixedValue with string node; @@ $$ == decoded inside
///   - Numeric literal → FixedValue with number node
///   - Boolean literal true/false → FixedValue with bool node
///   - Otherwise → FixedValue with string node
///
/// Ported from JLio's FunctionConverter / SplitText.GetChoppedElements.
/// </summary>
public class FunctionConverter<TNode>
{
    private readonly IFunctionsProvider<TNode> _functionsProvider;

    public FunctionConverter(IFunctionsProvider<TNode> functionsProvider)
        => _functionsProvider = functionsProvider;

    /// <summary>
    /// Parse a raw script value string into an IFunctionSupportedValue.
    /// Returns null only when a function expression refers to an unknown function.
    /// </summary>
    public IFunctionSupportedValue<TNode>? ParseValue(string rawValue, INodeAdapter<TNode> adapter)
    {
        if (string.IsNullOrEmpty(rawValue))
            return new FixedValue<TNode>(adapter.CreateString(""));

        // Escape sequences: double-prefix produces a literal string instead of triggering
        if (rawValue.StartsWith("=="))
            return new FixedValue<TNode>(adapter.CreateString("=" + rawValue.Substring(2)));
        if (rawValue.StartsWith("$$"))
            return new FixedValue<TNode>(adapter.CreateString("$" + rawValue.Substring(2)));
        if (rawValue.StartsWith("@@"))
            return new FixedValue<TNode>(adapter.CreateString("@" + rawValue.Substring(2)));

        if (rawValue.StartsWith("="))
            return ParseFunctionExpression(rawValue.Substring(1), adapter);

        if (rawValue.StartsWith("$") || rawValue.StartsWith("@"))
            return new PathValue<TNode>(rawValue);

        // Quoted string — @@, $$, == are escape sequences for literal @, $, = inside quotes
        if ((rawValue.StartsWith("'") && rawValue.EndsWith("'")) ||
            (rawValue.StartsWith("\"") && rawValue.EndsWith("\"")))
        {
            var content = rawValue.Substring(1, rawValue.Length - 2)
                .Replace("@@", "@")
                .Replace("$$", "$")
                .Replace("==", "=");
            return new FixedValue<TNode>(adapter.CreateString(content));
        }


        // Boolean
        if (bool.TryParse(rawValue, out var boolVal))
            return new FixedValue<TNode>(adapter.CreateBoolean(boolVal));

        // Numeric
        if (double.TryParse(rawValue,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var numVal))
            return new FixedValue<TNode>(adapter.CreateNumber(numVal));

        // Fall back to string literal
        return new FixedValue<TNode>(adapter.CreateString(rawValue));
    }

    private IFunctionSupportedValue<TNode>? ParseFunctionExpression(string expression, INodeAdapter<TNode> adapter)
    {
        var parenIdx = expression.IndexOf('(');
        if (parenIdx < 0)
        {
            // Bare name, no parens — treat as zero-arg function
            var bareFunc = _functionsProvider.GetFunction(expression.Trim());
            if (bareFunc == null) return null;
            bareFunc.SetArguments(new Arguments<TNode>());
            return new FunctionSupportedValue<TNode>(bareFunc);
        }

        var funcName = expression.Substring(0, parenIdx).Trim();
        var rest = expression.Substring(parenIdx + 1);

        // Strip trailing closing paren (balanced)
        if (rest.EndsWith(")"))
            rest = rest.Substring(0, rest.Length - 1);

        var function = _functionsProvider.GetFunction(funcName);
        if (function == null)
            return null;

        var argStrings = SplitArgs(rest);
        var arguments = new Arguments<TNode>();
        foreach (var arg in argStrings)
        {
            var trimmed = arg.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;
            // Path expressions inside function calls are treated as string literals
            // so that functions like fetch() and partial() can evaluate the path themselves.
            IFunctionSupportedValue<TNode>? argValue;
            if (trimmed.StartsWith("$") || trimmed.StartsWith("@"))
                argValue = new FixedValue<TNode>(adapter.CreateString(trimmed));
            else
                argValue = ParseValue(trimmed, adapter);
            if (argValue != null)
                arguments.Add(argValue);
        }

        function.SetArguments(arguments);
        return new FunctionSupportedValue<TNode>(function);
    }

    /// <summary>
    /// Split a comma-separated argument string, respecting nested parentheses and quotes.
    /// Ported from JLio's SplitText.GetChoppedElements.
    /// </summary>
    internal static List<string> SplitArgs(string argsStr)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;
        var inQuote = false;
        var quoteChar = '\0';

        foreach (var c in argsStr)
        {
            if (!inQuote && (c == '\'' || c == '"'))
            {
                inQuote = true;
                quoteChar = c;
                current.Append(c);
            }
            else if (inQuote && c == quoteChar)
            {
                inQuote = false;
                current.Append(c);
            }
            else if (!inQuote && c == '(')
            {
                depth++;
                current.Append(c);
            }
            else if (!inQuote && c == ')')
            {
                depth--;
                current.Append(c);
            }
            else if (!inQuote && depth == 0 && c == ',')
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }
}
