// A transcription of Newtonsoft.Json 13.0.4's JPath parser (Src/Newtonsoft.Json/Linq/JsonPath/JPath.cs,
// MIT licensed, Copyright (c) 2007 James Newton-King). It is deliberately NOT tidied: the quirks are the
// specification of this dialect — an unquoted member name is "everything up to the next . [ ( space",
// '..' before an indexer other than a name filter or a query silently drops the descent, a number inside
// a filter must be followed by a space or ')', and so on. Each is pinned by a differential test.
//
// Differences from the original are limited to error reporting: failures raise JsonPathException (kind
// Syntax, with the position) instead of JsonException, and conversions that Newtonsoft leaves to
// Convert.ToInt32 / long.TryParse report through the same exception.
#nullable disable
using System.Globalization;
using System.Text;

namespace TLio.JsonPath.Internal.Newtonsoft;

internal sealed class NewtonsoftParser
{
    private static readonly char[] FloatCharacters = ['.', 'E', 'e'];

    private readonly string _expression;
    private int _currentIndex;

    public List<NFilter> Filters { get; } = new();

    private NewtonsoftParser(string expression) => _expression = expression;

    public static List<NFilter> Parse(string expression)
    {
        var parser = new NewtonsoftParser(expression);
        try
        {
            parser.ParseMain();
        }
        catch (IndexOutOfRangeException)
        {
            // Newtonsoft reaches past the end of the text in a few malformed-query paths (for example a number
            // that runs to the end of the query) and lets the runtime exception escape. Still a syntax error.
            throw parser.Error("Path ended unexpectedly.");
        }

        return parser.Filters;
    }

    private JsonPathException Error(string message) => new(message, JsonPathErrorKind.Syntax, Math.Min(_currentIndex, _expression.Length));

    private void ParseMain()
    {
        var currentPartStartIndex = _currentIndex;

        EatWhitespace();

        if (_expression.Length == _currentIndex) return;

        if (_expression[_currentIndex] == '$')
        {
            if (_expression.Length == 1) return;

            // only increment position for "$." or "$["
            // otherwise assume property that starts with $
            var c = _expression[_currentIndex + 1];
            if (c == '.' || c == '[')
            {
                _currentIndex++;
                currentPartStartIndex = _currentIndex;
            }
        }

        if (!ParsePath(Filters, currentPartStartIndex, false))
        {
            var lastCharacterIndex = _currentIndex;

            EatWhitespace();

            if (_currentIndex < _expression.Length)
            {
                _currentIndex = lastCharacterIndex;
                throw Error("Unexpected character while parsing path: " + _expression[lastCharacterIndex]);
            }
        }
    }

    private bool ParsePath(List<NFilter> filters, int currentPartStartIndex, bool query)
    {
        var scan = false;
        var followingIndexer = false;
        var followingDot = false;

        var ended = false;
        while (_currentIndex < _expression.Length && !ended)
        {
            var currentChar = _expression[_currentIndex];

            switch (currentChar)
            {
                case '[':
                case '(':
                    if (_currentIndex > currentPartStartIndex)
                    {
                        var member = _expression.Substring(currentPartStartIndex, _currentIndex - currentPartStartIndex);
                        if (member == "*") member = null;

                        filters.Add(CreatePathFilter(member, scan));
                        scan = false;
                    }

                    filters.Add(ParseIndexer(currentChar, scan));
                    scan = false;

                    _currentIndex++;
                    currentPartStartIndex = _currentIndex;
                    followingIndexer = true;
                    followingDot = false;
                    break;
                case ']':
                case ')':
                    ended = true;
                    break;
                case ' ':
                    if (_currentIndex < _expression.Length) ended = true;
                    break;
                case '.':
                    if (_currentIndex > currentPartStartIndex)
                    {
                        var member = _expression.Substring(currentPartStartIndex, _currentIndex - currentPartStartIndex);
                        if (member == "*") member = null;

                        filters.Add(CreatePathFilter(member, scan));
                        scan = false;
                    }

                    if (_currentIndex + 1 < _expression.Length && _expression[_currentIndex + 1] == '.')
                    {
                        scan = true;
                        _currentIndex++;
                    }

                    _currentIndex++;
                    currentPartStartIndex = _currentIndex;
                    followingIndexer = false;
                    followingDot = true;
                    break;
                default:
                    if (query && (currentChar == '=' || currentChar == '<' || currentChar == '!' || currentChar == '>' || currentChar == '|' || currentChar == '&'))
                    {
                        ended = true;
                    }
                    else
                    {
                        if (followingIndexer) throw Error("Unexpected character following indexer: " + currentChar);

                        _currentIndex++;
                    }

                    break;
            }
        }

        var atPathEnd = _currentIndex == _expression.Length;

        if (_currentIndex > currentPartStartIndex)
        {
            var member = _expression.Substring(currentPartStartIndex, _currentIndex - currentPartStartIndex).TrimEnd();
            if (member == "*") member = null;
            filters.Add(CreatePathFilter(member, scan));
        }
        else
        {
            // no field name following dot in path and at end of base path/query
            if (followingDot && (atPathEnd || query)) throw Error("Unexpected end while parsing path.");
        }

        return atPathEnd;
    }

    private static NFilter CreatePathFilter(string member, bool scan) =>
        scan ? new NScanFilter(member) : new NFieldFilter(member);

    private NFilter ParseIndexer(char indexerOpenChar, bool scan)
    {
        _currentIndex++;

        var indexerCloseChar = indexerOpenChar == '[' ? ']' : ')';

        EnsureLength("Path ended with open indexer.");

        EatWhitespace();

        if (_expression[_currentIndex] == '\'') return ParseQuotedField(indexerCloseChar, scan);
        if (_expression[_currentIndex] == '?') return ParseQuery(indexerCloseChar, scan);
        return ParseArrayIndexer(indexerCloseChar);
    }

    private int ToInt32(string text)
    {
        // Convert.ToInt32(text, CultureInfo.InvariantCulture)
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw Error($"The input string '{text}' was not in a correct format.");
        return value;
    }

    private NFilter ParseArrayIndexer(char indexerCloseChar)
    {
        var start = _currentIndex;
        int? end = null;
        List<int> indexes = null;
        var colonCount = 0;
        int? startIndex = null;
        int? endIndex = null;
        int? step = null;

        while (_currentIndex < _expression.Length)
        {
            var currentCharacter = _expression[_currentIndex];

            if (currentCharacter == ' ')
            {
                end = _currentIndex;
                EatWhitespace();
                continue;
            }

            if (currentCharacter == indexerCloseChar)
            {
                var length = (end ?? _currentIndex) - start;

                if (indexes != null)
                {
                    if (length == 0) throw Error("Array index expected.");

                    var indexer = _expression.Substring(start, length);
                    indexes.Add(ToInt32(indexer));
                    return new NArrayMultipleIndexFilter(indexes);
                }

                if (colonCount > 0)
                {
                    if (length > 0)
                    {
                        var indexer = _expression.Substring(start, length);
                        var index = ToInt32(indexer);

                        if (colonCount == 1) endIndex = index;
                        else step = index;
                    }

                    return new NArraySliceFilter(startIndex, endIndex, step);
                }

                {
                    if (length == 0) throw Error("Array index expected.");

                    var indexer = _expression.Substring(start, length);
                    return new NArrayIndexFilter(ToInt32(indexer));
                }
            }

            if (currentCharacter == ',')
            {
                var length = (end ?? _currentIndex) - start;

                if (length == 0) throw Error("Array index expected.");

                indexes ??= new List<int>();

                var indexer = _expression.Substring(start, length);
                indexes.Add(ToInt32(indexer));

                _currentIndex++;

                EatWhitespace();

                start = _currentIndex;
                end = null;
            }
            else if (currentCharacter == '*')
            {
                _currentIndex++;
                EnsureLength("Path ended with open indexer.");
                EatWhitespace();

                if (_expression[_currentIndex] != indexerCloseChar)
                    throw Error("Unexpected character while parsing path indexer: " + currentCharacter);

                return new NArrayIndexFilter(null);
            }
            else if (currentCharacter == ':')
            {
                var length = (end ?? _currentIndex) - start;

                if (length > 0)
                {
                    var indexer = _expression.Substring(start, length);
                    var index = ToInt32(indexer);

                    if (colonCount == 0) startIndex = index;
                    else if (colonCount == 1) endIndex = index;
                    else step = index;
                }

                colonCount++;

                _currentIndex++;

                EatWhitespace();

                start = _currentIndex;
                end = null;
            }
            else if (!char.IsDigit(currentCharacter) && currentCharacter != '-')
            {
                throw Error("Unexpected character while parsing path indexer: " + currentCharacter);
            }
            else
            {
                if (end != null) throw Error("Unexpected character while parsing path indexer: " + currentCharacter);

                _currentIndex++;
            }
        }

        throw Error("Path ended with open indexer.");
    }

    private void EatWhitespace()
    {
        while (_currentIndex < _expression.Length)
        {
            if (_expression[_currentIndex] != ' ') break;
            _currentIndex++;
        }
    }

    private NFilter ParseQuery(char indexerCloseChar, bool scan)
    {
        _currentIndex++;
        EnsureLength("Path ended with open indexer.");

        if (_expression[_currentIndex] != '(')
            throw Error("Unexpected character while parsing path indexer: " + _expression[_currentIndex]);

        _currentIndex++;

        var expression = ParseExpression();

        _currentIndex++;
        EnsureLength("Path ended with open indexer.");
        EatWhitespace();

        if (_expression[_currentIndex] != indexerCloseChar)
            throw Error("Unexpected character while parsing path indexer: " + _expression[_currentIndex]);

        return scan ? new NQueryScanFilter(expression) : new NQueryFilter(expression);
    }

    private bool TryParseExpression(out List<NFilter> expressionPath)
    {
        if (_expression[_currentIndex] == '$') expressionPath = [NRootFilter.Instance];
        else if (_expression[_currentIndex] == '@') expressionPath = [];
        else
        {
            expressionPath = null;
            return false;
        }

        _currentIndex++;

        if (ParsePath(expressionPath, _currentIndex, true)) throw Error("Path ended with open query.");

        return true;
    }

    private JsonPathException CreateUnexpectedCharacterException() =>
        Error("Unexpected character while parsing path query: " + _expression[_currentIndex]);

    private object ParseSide()
    {
        EatWhitespace();

        if (TryParseExpression(out var expressionPath))
        {
            EatWhitespace();
            EnsureLength("Path ended with open query.");

            return expressionPath;
        }

        if (TryParseValue(out var value))
        {
            EatWhitespace();
            EnsureLength("Path ended with open query.");

            return value; // a Prim, boxed — Newtonsoft's `new JValue(value)`
        }

        throw CreateUnexpectedCharacterException();
    }

    private NExpression ParseExpression()
    {
        NExpression rootExpression = null;
        NCompositeExpression parentExpression = null;

        while (_currentIndex < _expression.Length)
        {
            var left = ParseSide();
            object right = null;

            NOp op;
            if (_expression[_currentIndex] == ')' || _expression[_currentIndex] == '|' || _expression[_currentIndex] == '&')
            {
                op = NOp.Exists;
            }
            else
            {
                op = ParseOperator();

                right = ParseSide();
            }

            var booleanExpression = new NBooleanExpression(op, left, right);

            if (_expression[_currentIndex] == ')')
            {
                if (parentExpression != null)
                {
                    parentExpression.Expressions.Add(booleanExpression);
                    return rootExpression;
                }

                return booleanExpression;
            }

            if (_expression[_currentIndex] == '&')
            {
                if (!Match("&&")) throw CreateUnexpectedCharacterException();

                if (parentExpression == null || parentExpression.Operator != NOp.And)
                {
                    var andExpression = new NCompositeExpression(NOp.And);

                    parentExpression?.Expressions.Add(andExpression);

                    parentExpression = andExpression;

                    rootExpression ??= parentExpression;
                }

                parentExpression.Expressions.Add(booleanExpression);
            }

            if (_expression[_currentIndex] == '|')
            {
                if (!Match("||")) throw CreateUnexpectedCharacterException();

                if (parentExpression == null || parentExpression.Operator != NOp.Or)
                {
                    var orExpression = new NCompositeExpression(NOp.Or);

                    parentExpression?.Expressions.Add(orExpression);

                    parentExpression = orExpression;

                    rootExpression ??= parentExpression;
                }

                parentExpression.Expressions.Add(booleanExpression);
            }
        }

        throw Error("Path ended with open query.");
    }

    private bool TryParseValue(out object value)
    {
        var currentChar = _expression[_currentIndex];
        if (currentChar == '\'')
        {
            value = Prim.FromString(ReadQuotedString());
            return true;
        }

        if (char.IsDigit(currentChar) || currentChar == '-')
        {
            var sb = new StringBuilder();
            sb.Append(currentChar);

            _currentIndex++;
            while (_currentIndex < _expression.Length)
            {
                currentChar = _expression[_currentIndex];
                if (currentChar == ' ' || currentChar == ')')
                {
                    var numberText = sb.ToString();

                    if (numberText.IndexOfAny(FloatCharacters) != -1)
                    {
                        var result = double.TryParse(numberText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var d);
                        value = Prim.FromDouble(d);
                        return result;
                    }
                    else
                    {
                        var result = long.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l);
                        value = Prim.FromLong(l);
                        return result;
                    }
                }

                sb.Append(currentChar);
                _currentIndex++;
            }
        }
        else if (currentChar == 't')
        {
            if (Match("true"))
            {
                value = Prim.FromBool(true);
                return true;
            }
        }
        else if (currentChar == 'f')
        {
            if (Match("false"))
            {
                value = Prim.FromBool(false);
                return true;
            }
        }
        else if (currentChar == 'n')
        {
            if (Match("null"))
            {
                value = Prim.Null;
                return true;
            }
        }
        else if (currentChar == '/')
        {
            value = Prim.FromString(ReadRegexString());
            return true;
        }

        value = null;
        return false;
    }

    private string ReadQuotedString()
    {
        var sb = new StringBuilder();

        _currentIndex++;
        while (_currentIndex < _expression.Length)
        {
            var currentChar = _expression[_currentIndex];
            if (currentChar == '\\' && _currentIndex + 1 < _expression.Length)
            {
                _currentIndex++;
                currentChar = _expression[_currentIndex];

                char resolvedChar;
                switch (currentChar)
                {
                    case 'b': resolvedChar = '\b'; break;
                    case 't': resolvedChar = '\t'; break;
                    case 'n': resolvedChar = '\n'; break;
                    case 'f': resolvedChar = '\f'; break;
                    case 'r': resolvedChar = '\r'; break;
                    case '\\':
                    case '"':
                    case '\'':
                    case '/':
                        resolvedChar = currentChar;
                        break;
                    default:
                        throw Error(@"Unknown escape character: \" + currentChar);
                }

                sb.Append(resolvedChar);

                _currentIndex++;
            }
            else if (currentChar == '\'')
            {
                _currentIndex++;
                return sb.ToString();
            }
            else
            {
                _currentIndex++;
                sb.Append(currentChar);
            }
        }

        throw Error("Path ended with an open string.");
    }

    private string ReadRegexString()
    {
        var startIndex = _currentIndex;

        _currentIndex++;
        while (_currentIndex < _expression.Length)
        {
            var currentChar = _expression[_currentIndex];

            // handle escaped / character
            if (currentChar == '\\' && _currentIndex + 1 < _expression.Length)
            {
                _currentIndex += 2;
            }
            else if (currentChar == '/')
            {
                _currentIndex++;

                while (_currentIndex < _expression.Length)
                {
                    currentChar = _expression[_currentIndex];

                    if (char.IsLetter(currentChar)) _currentIndex++;
                    else break;
                }

                return _expression.Substring(startIndex, _currentIndex - startIndex);
            }
            else
            {
                _currentIndex++;
            }
        }

        throw Error("Path ended with an open regex.");
    }

    private bool Match(string s)
    {
        var currentPosition = _currentIndex;
        for (var i = 0; i < s.Length; i++)
        {
            if (currentPosition < _expression.Length && _expression[currentPosition] == s[i]) currentPosition++;
            else return false;
        }

        _currentIndex = currentPosition;
        return true;
    }

    private NOp ParseOperator()
    {
        if (_currentIndex + 1 >= _expression.Length) throw Error("Path ended with open query.");

        if (Match("===")) return NOp.StrictEquals;
        if (Match("==")) return NOp.Equals;
        if (Match("=~")) return NOp.RegexEquals;
        if (Match("!==")) return NOp.StrictNotEquals;
        if (Match("!=") || Match("<>")) return NOp.NotEquals;
        if (Match("<=")) return NOp.LessThanOrEquals;
        if (Match("<")) return NOp.LessThan;
        if (Match(">=")) return NOp.GreaterThanOrEquals;
        if (Match(">")) return NOp.GreaterThan;

        throw Error("Could not read query operator.");
    }

    private NFilter ParseQuotedField(char indexerCloseChar, bool scan)
    {
        List<string> fields = null;

        while (_currentIndex < _expression.Length)
        {
            var field = ReadQuotedString();

            EatWhitespace();
            EnsureLength("Path ended with open indexer.");

            if (_expression[_currentIndex] == indexerCloseChar)
            {
                if (fields != null)
                {
                    fields.Add(field);
                    return scan ? new NScanMultipleFilter(fields) : new NFieldMultipleFilter(fields);
                }

                return CreatePathFilter(field, scan);
            }

            if (_expression[_currentIndex] == ',')
            {
                _currentIndex++;
                EatWhitespace();

                fields ??= new List<string>();

                fields.Add(field);
            }
            else
            {
                throw Error("Unexpected character while parsing path indexer: " + _expression[_currentIndex]);
            }
        }

        throw Error("Path ended with open indexer.");
    }

    private void EnsureLength(string message)
    {
        if (_currentIndex >= _expression.Length) throw Error(message);
    }
}
