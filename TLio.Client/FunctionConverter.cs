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
///   - Quoted string 'value' or "value":
///       - Inner starts with "=" (not "==") → nested function expression (enables dynamic paths)
///       - Otherwise → FixedValue; @@, $$, == and doubled quote char ('' / "") decoded inside
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
    /// Returns a <see cref="NotFoundFunctionValue{TNode}"/> sentinel when a function expression refers to an unknown function.
    /// <paramref name="warnCallback"/> is invoked (when provided) if a likely notation mistake is detected,
    /// e.g. <c>@field</c> instead of the required <c>@.field</c> form.
    /// </summary>
    public IFunctionSupportedValue<TNode>? ParseValue(string rawValue, INodeAdapter<TNode> adapter,
        Action<string>? warnCallback = null)
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

        if (rawValue.StartsWith("$"))
            return new PathValue<TNode>(rawValue);

        if (rawValue.StartsWith("@"))
        {
            // @@ was already handled above as an escape sequence.
            // @. is the required form for relative paths in JSON/YAML.
            // Anything else (e.g. @field) is likely a missing dot — warn and still produce PathValue.
            if (!rawValue.StartsWith("@."))
                warnCallback?.Invoke(
                    $"Path '{rawValue}' is missing the required dot — did you mean '@.{rawValue.Substring(1)}'? " +
                    "Relative paths in JSON/YAML require the '@.' prefix.");
            return new PathValue<TNode>(rawValue);
        }

        // Quoted string — @@, $$, == are escape sequences for literal @, $, = inside quotes.
        // '' (or "") is an escape for a literal quote char inside a quoted string.
        // If the inner content (before escape processing) starts with = (but not ==),
        // it is treated as a nested function expression whose result is used as-is.
        // This enables dynamic path computation: =fetch('=concat($.a, $.b)')
        if ((rawValue.StartsWith("'") && rawValue.EndsWith("'")) ||
            (rawValue.StartsWith("\"") && rawValue.EndsWith("\"")))
        {
            var quoteChar = rawValue[0];
            var inner = rawValue.Substring(1, rawValue.Length - 2);

            // If inner content starts with = (but not ==), treat as nested function expression.
            // Unescape doubled quote chars first so inner quoted-string args are correct.
            if (inner.Length > 0 && inner[0] == '=' && (inner.Length < 2 || inner[1] != '='))
            {
                var escapedQuote = new string(quoteChar, 2); // '' or ""
                // Unescape only the doubled quote chars; other escapes (@@, $$, ==) are handled
                // by the function parser for each inner argument individually.
                var innerExpr = inner.Substring(1).Replace(escapedQuote, quoteChar.ToString());
                return ParseFunctionExpression(innerExpr, adapter);
            }

            // Apply escape sequences: doubled quote char → literal quote; @@, $$, == → @, $, =
            var content = inner
                .Replace(new string(quoteChar, 2), quoteChar.ToString())
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
            var bareName = expression.Trim();
            var bareFunc = _functionsProvider.GetFunction(bareName);
            if (bareFunc == null) return new NotFoundFunctionValue<TNode>(bareName);
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
            return new NotFoundFunctionValue<TNode>(funcName);

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
    /// A doubled quote char inside a quoted string ('' or "") is treated as an escape for a
    /// literal quote, keeping the parser inside the current quoted token rather than ending it.
    /// Ported from JLio's SplitText.GetChoppedElements.
    /// </summary>
    internal static List<string> SplitArgs(string argsStr)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var depth = 0;
        var inQuote = false;
        var quoteChar = '\0';

        for (int i = 0; i < argsStr.Length; i++)
        {
            var c = argsStr[i];

            if (!inQuote && (c == '\'' || c == '"'))
            {
                inQuote = true;
                quoteChar = c;
                current.Append(c);
            }
            else if (inQuote && c == quoteChar)
            {
                // A doubled quote char ('' or "") is an escape for a literal quote inside
                // the string — keep the parser in-quote and append both characters.
                if (i + 1 < argsStr.Length && argsStr[i + 1] == quoteChar)
                {
                    current.Append(c);
                    current.Append(c);
                    i++; // skip the second quote of the pair
                }
                else
                {
                    inQuote = false;
                    current.Append(c);
                }
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
