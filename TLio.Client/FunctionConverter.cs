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
///   - Otherwise → FixedValue with string node
///
/// JSON literals (numbers, true/false, null) are only recognised in ARGUMENT position, where
/// there is no other way to write them: =substring($.a, 0, 3). At value level the JSON document
/// already expresses them natively ("value": 42), so a JSON *string* stays a string — "007"
/// must not silently become the number 7.
///
/// Argument parsing rules (inside "name(...)"):
///   - Only the OUTERMOST expression must start with "=". For arguments the "=" is optional:
///     an unquoted argument of the form knownFunction(...) is parsed as a nested call, so
///     "=concat(fetch($.a), ' ', toString($.b))" and "=concat(=fetch($.a), ...)" are equivalent.
///   - The name must be registered in the functions provider; unknown names keep the historic
///     literal-string fallback (and raise a warning via warnCallback).
///   - Quoting always wins: 'fetch($.a)' stays the literal text fetch($.a).
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
    /// e.g. <c>@field</c> instead of the required <c>@.field</c> form, or a nested call to an
    /// unregistered function. Warnings raised here are collected by <see cref="CommandConverter{TNode}"/>
    /// and logged when the script executes.
    /// <paramref name="asArgument"/> selects argument-position parsing, where bare JSON literals
    /// (<c>42</c>, <c>true</c>, <c>null</c>) are recognised.
    /// </summary>
    public IFunctionSupportedValue<TNode>? ParseValue(string rawValue, INodeAdapter<TNode> adapter,
        Action<string>? warnCallback = null, bool asArgument = false)
    {
        if (string.IsNullOrEmpty(rawValue))
            return new FixedValue<TNode>(adapter.CreateString(""), rawValue);

        // Escape sequences: double-prefix produces a literal string instead of triggering
        if (rawValue.StartsWith("=="))
            return new FixedValue<TNode>(adapter.CreateString("=" + rawValue.Substring(2)), rawValue);
        if (rawValue.StartsWith("$$"))
            return new FixedValue<TNode>(adapter.CreateString("$" + rawValue.Substring(2)), rawValue);
        if (rawValue.StartsWith("@@"))
            return new FixedValue<TNode>(adapter.CreateString("@" + rawValue.Substring(2)), rawValue);

        if (rawValue.StartsWith("="))
            return ParseFunctionExpression(rawValue.Substring(1), adapter, warnCallback);

        if (rawValue.StartsWith("$"))
            return new PathValue<TNode>(rawValue);

        if (rawValue.StartsWith("@"))
        {
            WarnIfRelativePathIsMissingDot(rawValue, warnCallback);
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
                return ParseFunctionExpression(innerExpr, adapter, warnCallback);
            }

            // Apply escape sequences: doubled quote char → literal quote; @@, $$, == → @, $, =
            var content = inner
                .Replace(new string(quoteChar, 2), quoteChar.ToString())
                .Replace("@@", "@")
                .Replace("$$", "$")
                .Replace("==", "=");
            return new FixedValue<TNode>(adapter.CreateString(content), rawValue);
        }

        // Bare JSON literals are argument-position only — see the class remarks. An array
        // literal is the same idea extended to a list: =scriptpath(*, ['object','primitive'],
        // true) has no other way to write a constant list of names inline. Elements are
        // constants only — a nested path or function inside the brackets has no execution
        // context yet at parse time, so it falls back to its raw text instead of being dropped
        // silently.
        if (asArgument && rawValue.StartsWith("[") && rawValue.EndsWith("]"))
        {
            var array = adapter.CreateArray();
            foreach (var element in SplitArgs(rawValue.Substring(1, rawValue.Length - 2)))
            {
                var trimmedElement = element.Trim();
                if (trimmedElement.Length == 0) continue;

                var elementValue = ParseValue(trimmedElement, adapter, warnCallback, asArgument: true);
                adapter.AppendToArray(array,
                    elementValue is FixedValue<TNode> fixedElement
                        ? fixedElement.Node
                        : adapter.CreateString(trimmedElement));
            }
            return new FixedValue<TNode>(array, rawValue);
        }

        if (asArgument)
        {
            if (bool.TryParse(rawValue, out var boolVal))
                return new FixedValue<TNode>(adapter.CreateBoolean(boolVal), rawValue);

            // Integral first, so =substring($.a, 0, 3) does not carry 0.0 / 3.0 around.
            if (long.TryParse(rawValue,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var intVal))
                return new FixedValue<TNode>(adapter.CreateValue(intVal), rawValue);

            if (double.TryParse(rawValue,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var numVal))
                return new FixedValue<TNode>(adapter.CreateNumber(numVal), rawValue);

            if (rawValue == "null")
                return new FixedValue<TNode>(adapter.CreateNull(), rawValue);
        }

        // Fall back to string literal
        return new FixedValue<TNode>(adapter.CreateString(rawValue), rawValue);
    }

    private IFunctionSupportedValue<TNode>? ParseFunctionExpression(string expression, INodeAdapter<TNode> adapter,
        Action<string>? warnCallback = null)
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
            {
                // The path is handed to the function as a string, so ParseValue never sees it —
                // run the same notation check here.
                if (trimmed.StartsWith("@") && !trimmed.StartsWith("@@"))
                    WarnIfRelativePathIsMissingDot(trimmed, warnCallback);
                argValue = new FixedValue<TNode>(adapter.CreateString(trimmed), trimmed);
            }
            else if (TryGetInnerCallName(trimmed, out var innerName))
            {
                // Inner (nested) call: the leading "=" is optional here — only the
                // outermost expression is required to carry it. An unknown name is not
                // a call at all; keep the historic literal-string fallback but warn,
                // because that silently turns a typo into text.
                if (_functionsProvider.GetFunction(innerName) != null)
                    argValue = ParseFunctionExpression(trimmed, adapter, warnCallback);
                else
                {
                    warnCallback?.Invoke(
                        $"Argument '{trimmed}' looks like a call to '{innerName}', but no such function is registered — " +
                        "it is treated as a literal string. Check the spelling, or register the function pack that provides it. " +
                        $"To keep it as text, quote it: '{trimmed}'.");
                    argValue = ParseValue(trimmed, adapter, warnCallback, asArgument: true);
                }
            }
            else
                argValue = ParseValue(trimmed, adapter, warnCallback, asArgument: true);
            if (argValue != null)
                arguments.Add(argValue);
        }

        function.SetArguments(arguments);
        return new FunctionSupportedValue<TNode>(function);
    }

    /// <summary>
    /// <c>@.</c> is the required form for relative paths in JSON/YAML. Anything else
    /// (e.g. <c>@field</c>) is almost certainly a missing dot — the path is still produced,
    /// but the author gets told why it will not resolve. XML XPath attribute selectors
    /// (<c>@id</c>) only occur inside <c>[...]</c> predicates, which never reach here.
    /// </summary>
    private static void WarnIfRelativePathIsMissingDot(string path, Action<string>? warnCallback)
    {
        if (path.StartsWith("@.")) return;
        warnCallback?.Invoke(
            $"Path '{path}' is missing the required dot — did you mean '@.{path.Substring(1)}'? " +
            "Relative paths in JSON/YAML require the '@.' prefix.");
    }

    /// <summary>
    /// Determine whether an unquoted function argument is itself a call expression written
    /// without the optional leading "=" — e.g. <c>fetch($.a)</c> inside <c>=concat(fetch($.a), ' ')</c>.
    ///
    /// The whole argument must be a single balanced call: an identifier, then "(" … ")" where the
    /// closing paren is the last character. That excludes text that merely contains parentheses
    /// (<c>hello (world)</c>) or two calls side by side (<c>a(1) b(2)</c>), which stay literals.
    /// The caller still checks that <paramref name="functionName"/> is registered before treating
    /// it as a call, so plain text is never silently reinterpreted.
    /// </summary>
    internal static bool TryGetInnerCallName(string text, out string functionName)
    {
        functionName = string.Empty;

        var parenIdx = text.IndexOf('(');
        if (parenIdx <= 0 || !text.EndsWith(")")) return false;

        var name = text.Substring(0, parenIdx).TrimEnd();
        if (name.Length == 0) return false;
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;
        foreach (var c in name)
            if (!char.IsLetterOrDigit(c) && c != '_') return false;

        // The '(' at parenIdx must be closed by the final ')' — quotes are skipped so a
        // paren inside a string argument does not affect the balance.
        var depth = 0;
        var inQuote = false;
        var quoteChar = '\0';
        for (int i = parenIdx; i < text.Length; i++)
        {
            var c = text[i];

            if (!inQuote && (c == '\'' || c == '"'))
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (inQuote && c == quoteChar)
            {
                // Doubled quote char is an escaped literal quote — stay in-quote.
                if (i + 1 < text.Length && text[i + 1] == quoteChar) i++;
                else inQuote = false;
            }
            else if (!inQuote && c == '(')
            {
                depth++;
            }
            else if (!inQuote && c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    if (i != text.Length - 1) return false;
                    functionName = name;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Split a comma-separated argument string, respecting nested parentheses, brackets and
    /// quotes — a comma inside <c>(...)</c> (a nested call) or <c>[...]</c> (an array literal)
    /// does not end the argument. A doubled quote char inside a quoted string ('' or "") is
    /// treated as an escape for a literal quote, keeping the parser inside the current quoted
    /// token rather than ending it.
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
            else if (!inQuote && (c == '(' || c == '['))
            {
                depth++;
                current.Append(c);
            }
            else if (!inQuote && (c == ')' || c == ']'))
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
