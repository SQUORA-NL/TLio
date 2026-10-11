using System.Globalization;

namespace TLio.Client;

/// <summary>
/// Parser for infix arithmetic inside function-value strings:
/// <c>=round($.principal * $.rate * $.yearFraction, 2)</c>.
///
/// This class only produces a syntax tree. <see cref="FunctionConverter{TNode}"/> turns the tree
/// into the existing function objects (<c>sum</c>, <c>subtract</c>, <c>multiply</c>, <c>divide</c>,
/// <c>modulo</c>, <c>pow</c>), so there is no second evaluator and nothing new at run time.
///
/// The rules that keep it unambiguous (operator characters are legal inside paths):
///   - A binary operator (<c>+ - * / % ^</c>) needs whitespace on BOTH sides. <c>$.a*$.b</c> is one
///     path, not a multiplication; <c>$.unit-price</c> is one path, not a subtraction.
///   - Operands are numbers, paths (<c>$</c>, <c>@</c>, or <c>/</c> for XML), 'quoted strings' and
///     calls <c>name(...)</c>, plus parentheses. A bare word is never a value.
///   - A path ends at the first whitespace, <c>,</c> or <c>)</c> outside <c>[...]</c>.
///   - Unary minus is written attached to its operand: <c>-5</c>, <c>-$.a</c>, <c>-(1 + 2)</c>.
///   - Precedence, high to low: <c>^</c> (right-assoc), unary <c>-</c>, <c>* / %</c>, <c>+ -</c>.
/// </summary>
internal static class InfixExpression
{
    private const int MaxDepth = 64;

    internal enum LeafKind { Number, Path, Quoted, Call }

    internal abstract record Node;
    internal sealed record Leaf(LeafKind Kind, string Text) : Node;
    internal sealed record Negate(Node Operand) : Node;
    internal sealed record Binary(char Op, Node Left, Node Right) : Node;

    internal sealed class InfixSyntaxException : Exception
    {
        public InfixSyntaxException(string message) : base(message) { }
    }

    private enum TokenKind { Operand, Op, Neg, LParen, RParen }

    private readonly record struct Token(TokenKind Kind, string Text = "", LeafKind Leaf = LeafKind.Number);

    internal static bool IsOperatorChar(char c) => c is '+' or '-' or '*' or '/' or '%' or '^';

    /// <summary>
    /// Cheap pre-check: is there a whitespace-delimited operator at the top level of
    /// <paramref name="text"/>? Quoted text, <c>[...]</c> and <c>(...)</c> hide their contents, so a
    /// plain call such as <c>concat($.a, ' - ', $.b)</c> is never mistaken for arithmetic.
    /// </summary>
    internal static bool LooksLikeInfix(string text)
    {
        var depth = 0;
        var inQuote = false;
        var quoteChar = '\0';

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuote)
            {
                if (c == quoteChar)
                {
                    if (i + 1 < text.Length && text[i + 1] == quoteChar) i++;
                    else inQuote = false;
                }
                continue;
            }

            if (c is '\'' or '"') { inQuote = true; quoteChar = c; }
            else if (c is '(' or '[') depth++;
            else if (c is ')' or ']') depth--;
            else if (depth == 0 && IsOperatorChar(c)
                     && i > 0 && char.IsWhiteSpace(text[i - 1])
                     && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                return true;
        }

        return false;
    }

    /// <summary>Parses <paramref name="text"/> or throws <see cref="InfixSyntaxException"/>.</summary>
    internal static Node Parse(string text)
    {
        var parser = new Parser(Tokenize(text));
        return parser.ParseAll();
    }

    // ── Tokenizer ────────────────────────────────────────────────────────────

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        var i = 0;
        var expectOperand = true;

        while (true)
        {
            var before = i;
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            if (i >= s.Length) break;

            var spaceBefore = i > before || before == 0;
            var c = s[i];

            if (expectOperand)
            {
                if (c == '(')
                {
                    tokens.Add(new Token(TokenKind.LParen));
                    i++;
                }
                else if (c == '-' && i + 1 < s.Length && !char.IsWhiteSpace(s[i + 1]))
                {
                    tokens.Add(new Token(TokenKind.Neg));
                    i++;
                }
                else
                {
                    tokens.Add(ReadOperand(s, ref i));
                    expectOperand = false;
                }
            }
            else if (c == ')')
            {
                tokens.Add(new Token(TokenKind.RParen));
                i++;
            }
            else if (IsOperatorChar(c))
            {
                if (!spaceBefore || i + 1 >= s.Length || !char.IsWhiteSpace(s[i + 1]))
                    throw new InfixSyntaxException(
                        $"operator '{c}' needs whitespace on both sides (a path may contain '{c}', so '$.a{c}$.b' is read as one path)");
                tokens.Add(new Token(TokenKind.Op, c.ToString()));
                i++;
                expectOperand = true;
            }
            else
            {
                throw new InfixSyntaxException(
                    $"expected an operator (+ - * / % ^) but found '{s.Substring(i, System.Math.Min(12, s.Length - i))}'");
            }
        }

        if (tokens.Count == 0) throw new InfixSyntaxException("the expression is empty");
        if (expectOperand) throw new InfixSyntaxException("the expression ends where a value is expected");
        return tokens;
    }

    private static Token ReadOperand(string s, ref int i)
    {
        var start = i;
        var c = s[i];

        if (c is '\'' or '"')
        {
            i++;
            while (true)
            {
                if (i >= s.Length) throw new InfixSyntaxException("a quoted string is not closed");
                if (s[i] == c)
                {
                    if (i + 1 < s.Length && s[i + 1] == c) { i += 2; continue; }
                    i++;
                    break;
                }
                i++;
            }
            return new Token(TokenKind.Operand, s.Substring(start, i - start), LeafKind.Quoted);
        }

        if (c is '$' or '@' or '/')
        {
            var depth = 0;
            var inQuote = false;
            var quoteChar = '\0';
            while (i < s.Length)
            {
                var ch = s[i];
                if (inQuote)
                {
                    if (ch == quoteChar) inQuote = false;
                }
                else if (depth > 0 && ch is '\'' or '"')
                {
                    inQuote = true;
                    quoteChar = ch;
                }
                else if (ch == '[') depth++;
                else if (ch == ']') depth--;
                else if (depth == 0 && (char.IsWhiteSpace(ch) || ch is ',' or ')')) break;
                i++;
            }
            if (depth != 0 || inQuote) throw new InfixSyntaxException("a '[' in a path is not closed");
            return new Token(TokenKind.Operand, s.Substring(start, i - start), LeafKind.Path);
        }

        if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
        {
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
            if (i < s.Length && s[i] is 'e' or 'E')
            {
                var j = i + 1;
                if (j < s.Length && s[j] is '+' or '-') j++;
                if (j < s.Length && char.IsDigit(s[j]))
                {
                    while (j < s.Length && char.IsDigit(s[j])) j++;
                    i = j;
                }
            }
            var text = s.Substring(start, i - start);
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                throw new InfixSyntaxException($"'{text}' is not a number");
            return new Token(TokenKind.Operand, text, LeafKind.Number);
        }

        // "=name(...)" is allowed for a call, exactly as in an argument list.
        if (c == '=' && i + 1 < s.Length && (char.IsLetter(s[i + 1]) || s[i + 1] == '_'))
        {
            i++;
            start = i;
            c = s[i];
        }

        if (char.IsLetter(c) || c == '_')
        {
            while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
            var name = s.Substring(start, i - start);

            if (i >= s.Length || s[i] != '(')
                throw new InfixSyntaxException(
                    $"'{name}' is not a value — write a path ($.{name}), a number, a 'quoted string' or a call {name}(...)");

            var depth = 0;
            var inQuote = false;
            var quoteChar = '\0';
            for (; i < s.Length; i++)
            {
                var ch = s[i];
                if (inQuote)
                {
                    if (ch == quoteChar)
                    {
                        if (i + 1 < s.Length && s[i + 1] == quoteChar) i++;
                        else inQuote = false;
                    }
                }
                else if (ch is '\'' or '"') { inQuote = true; quoteChar = ch; }
                else if (ch == '(') depth++;
                else if (ch == ')' && --depth == 0) { i++; break; }
            }
            if (depth != 0) throw new InfixSyntaxException($"the call '{name}(' is not closed");
            return new Token(TokenKind.Operand, s.Substring(start, i - start), LeafKind.Call);
        }

        throw new InfixSyntaxException($"unexpected '{c}' where a value is expected");
    }

    // ── Parser (precedence climbing) ─────────────────────────────────────────

    private sealed class Parser
    {
        private const int UnaryBindingPower = 5;
        private readonly List<Token> _tokens;
        private int _pos;
        private int _depth;

        public Parser(List<Token> tokens) => _tokens = tokens;

        public Node ParseAll()
        {
            var node = ParseExpression(0);
            if (_pos < _tokens.Count)
                throw new InfixSyntaxException("unbalanced parentheses");
            return node;
        }

        private Node ParseExpression(int minBindingPower)
        {
            if (++_depth > MaxDepth) throw new InfixSyntaxException("the expression is nested too deeply");

            var token = _tokens[_pos++];
            Node left;
            switch (token.Kind)
            {
                case TokenKind.Operand:
                    left = new Leaf(token.Leaf, token.Text);
                    break;
                case TokenKind.LParen:
                    left = ParseExpression(0);
                    if (_pos >= _tokens.Count || _tokens[_pos].Kind != TokenKind.RParen)
                        throw new InfixSyntaxException("unbalanced parentheses");
                    _pos++;
                    break;
                case TokenKind.Neg:
                    var operand = ParseExpression(UnaryBindingPower);
                    // -5 stays a number; -5 ^ 2 is -(5 ^ 2) because ^ binds tighter than unary minus.
                    left = operand is Leaf { Kind: LeafKind.Number } n && !n.Text.StartsWith('-')
                        ? new Leaf(LeafKind.Number, "-" + n.Text)
                        : new Negate(operand);
                    break;
                default:
                    throw new InfixSyntaxException("unbalanced parentheses");
            }

            while (_pos < _tokens.Count && _tokens[_pos].Kind == TokenKind.Op)
            {
                var op = _tokens[_pos].Text[0];
                var (leftPower, rightPower) = BindingPower(op);
                if (leftPower <= minBindingPower) break;
                _pos++;
                var right = ParseExpression(rightPower);
                left = new Binary(op, left, right);
            }

            _depth--;
            return left;
        }

        private static (int Left, int Right) BindingPower(char op) => op switch
        {
            '+' or '-' => (1, 2),
            '*' or '/' or '%' => (3, 4),
            _ => (8, 7), // '^', right-associative
        };
    }
}
