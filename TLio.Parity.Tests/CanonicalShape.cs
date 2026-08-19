using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Newtonsoft.Json.Linq;

namespace TLio.Parity.Tests;

/// <summary>
/// The canonical shape a JSON document takes in the other two formats, and the canonical
/// spelling a JSON script takes in their notations.
///
/// <para>Every parity fixture is written once, in JSON. This class is what turns it into the
/// XML and YAML the same fixture should produce, so the three suites cannot drift: if XML
/// stops agreeing with JSON, the fixture fails rather than quietly testing something else.</para>
///
/// <para>The mapping is the one <see cref="TLio.Xml.XmlNodeAdapter"/> reads documents in:</para>
/// <list type="table">
///   <listheader><term>JSON</term><description>XML</description></listheader>
///   <item><term><c>{"k":"v"}</c></term><description><c>&lt;k&gt;v&lt;/k&gt;</c></description></item>
///   <item><term><c>{"k":{…}}</c></term><description><c>&lt;k&gt;</c> + one child element per property</description></item>
///   <item><term><c>{"k":[1,2]}</c></term><description><c>&lt;k&gt;&lt;item&gt;1&lt;/item&gt;&lt;item&gt;2&lt;/item&gt;&lt;/k&gt;</c></description></item>
///   <item><term><c>{"k":null}</c></term><description><c>&lt;k/&gt;</c> — as do <c>""</c>, <c>{}</c> and <c>[]</c>,
///     which XML cannot tell apart</description></item>
/// </list>
/// </summary>
public static class CanonicalShape
{
    /// <summary>Element name of the document element a JSON root object becomes.</summary>
    public const string RootName = "root";

    private const string ItemName = TLio.Xml.XmlNodeAdapter.DefaultItemName;

    // ── JSON → XML ────────────────────────────────────────────────────────────

    public static string ToXmlDocument(JToken json) =>
        ToXml(json, RootName).ToString(SaveOptions.DisableFormatting);

    private static XElement ToXml(JToken token, string name) => token.Type switch
    {
        JTokenType.Object => new XElement(name,
            ((JObject)token).Properties().Select(p => ToXml(p.Value, p.Name))),

        JTokenType.Array => new XElement(name,
            ((JArray)token).Select(item => ToXml(item, ItemName))),

        // null, "" and the empty containers are all the empty element.
        JTokenType.Null => new XElement(name),

        JTokenType.Boolean => new XElement(name, (bool)token! ? "true" : "false"),

        _ => new XElement(name, Text(token))
    };

    // ── JSON → YAML ───────────────────────────────────────────────────────────

    public static string ToYamlDocument(JToken json) => WriteYaml(json, 0).TrimEnd() + "\n";

    private static string WriteYaml(JToken token, int indent)
    {
        var pad = new string(' ', indent);

        switch (token)
        {
            case JObject obj when obj.Properties().Any():
            {
                var sb = new StringBuilder();
                foreach (var p in obj.Properties())
                {
                    if (IsNestedBlock(p.Value))
                        sb.Append(pad).Append(YamlKey(p.Name)).Append(":\n")
                          .Append(WriteYaml(p.Value, indent + 2));
                    else
                        sb.Append(pad).Append(YamlKey(p.Name)).Append(": ")
                          .Append(YamlScalar(p.Value)).Append('\n');
                }
                return sb.ToString();
            }

            case JArray arr when arr.Count > 0:
            {
                var sb = new StringBuilder();
                foreach (var item in arr)
                {
                    if (IsNestedBlock(item))
                        // The first line of the block sits on the dash; the rest is already indented.
                        sb.Append(pad).Append("- ").Append(WriteYaml(item, indent + 2).AsSpan(indent + 2));
                    else
                        sb.Append(pad).Append("- ").Append(YamlScalar(item)).Append('\n');
                }
                return sb.ToString();
            }

            default:
                return pad + YamlScalar(token) + "\n";
        }
    }

    private static bool IsNestedBlock(JToken t) =>
        (t.Type == JTokenType.Object && ((JObject)t).Properties().Any()) ||
        (t.Type == JTokenType.Array && ((JArray)t).Count > 0);

    private static string YamlScalar(JToken t) => t.Type switch
    {
        JTokenType.Null    => "null",
        JTokenType.Boolean => (bool)t! ? "true" : "false",
        JTokenType.Object  => "{}",
        JTokenType.Array   => "[]",
        JTokenType.String  => QuoteYaml(t.ToString()),
        _                  => Text(t)
    };

    private static string YamlKey(string name) => QuoteYaml(name);

    /// <summary>
    /// Quotes anything a YAML reader would otherwise read as something other than the string
    /// it is — a number, a boolean, a null, or a token with structural meaning.
    /// </summary>
    private static string QuoteYaml(string s)
    {
        var needsQuotes =
            s.Length == 0 ||
            s is "null" or "~" or "true" or "false" or "yes" or "no" or "on" or "off" ||
            double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _) ||
            s.IndexOfAny(":#{}[]&*!|>'\"%@`,".ToCharArray()) >= 0 ||
            char.IsWhiteSpace(s[0]) || char.IsWhiteSpace(s[^1]);

        return needsQuotes ? "'" + s.Replace("'", "''") + "'" : s;
    }

    // ── Script notation ───────────────────────────────────────────────────────

    /// <summary>
    /// The XML spelling of a JSON script. Paths are rewritten to XPath, nested scripts and
    /// structured values become child elements, everything else becomes an attribute.
    /// </summary>
    public static string ToXmlScript(string scriptJson)
    {
        var script = new XElement("script");
        foreach (var command in JArray.Parse(scriptJson).OfType<JObject>())
            script.Add(ToXmlCommand(command));
        return script.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement ToXmlCommand(JObject command)
    {
        var el = new XElement(command["command"]!.ToString());

        foreach (var p in command.Properties())
        {
            if (p.Name == "command") continue;

            if (p.Name == "value")
            {
                el.Add(p.Value.Type is JTokenType.Object or JTokenType.Array
                    ? ToXml(p.Value, "value")
                    : p.Value.Type == JTokenType.Null
                        ? new XElement("value")
                        : new XElement("value", RewritePaths(Text(p.Value))));
            }
            else if (p.Value.Type == JTokenType.Array && IsCommandList(p.Value))
            {
                var nested = new XElement(p.Name);
                foreach (var inner in ((JArray)p.Value).OfType<JObject>())
                    nested.Add(ToXmlCommand(inner));
                el.Add(nested);
            }
            else if (p.Value.Type is JTokenType.Object or JTokenType.Array)
            {
                el.Add(ToXml(RewritePathsDeep(p.Value), p.Name));
            }
            else
            {
                el.SetAttributeValue(p.Name, RewritePaths(Text(p.Value)));
            }
        }

        return el;
    }

    private static bool IsCommandList(JToken t) =>
        t is JArray a && a.Count > 0 && a.All(i => i is JObject o && o["command"] != null);

    /// <summary>The YAML spelling of a JSON script — the same object graph in YAML block style.</summary>
    public static string ToYamlScript(string scriptJson) => ToYamlDocument(JArray.Parse(scriptJson));

    // ── Path rewriting ────────────────────────────────────────────────────────

    /// <summary>
    /// Rewrites every JSONPath in a string to its XPath equivalent, so one fixture can drive
    /// both notations: <c>$.a.b</c> → <c>/root/a/b</c>, <c>$.items[0].n</c> →
    /// <c>/root/items/item[1]/n</c>, <c>$..n</c> → <c>//n</c>, <c>$</c> → <c>/</c>.
    /// </summary>
    public static string RewritePaths(string text) =>
        System.Text.RegularExpressions.Regex.Replace(
            text, @"\$(\.\.?[A-Za-z0-9_]+(\[[0-9*]+\])?)*(?![A-Za-z0-9_])", m => ToXPath(m.Value));

    private static JToken RewritePathsDeep(JToken token) => token switch
    {
        JObject o => new JObject(o.Properties().Select(p => new JProperty(p.Name, RewritePathsDeep(p.Value)))),
        JArray a  => new JArray(a.Select(RewritePathsDeep)),
        _ when token.Type == JTokenType.String => new JValue(RewritePaths(token.ToString())),
        _ => token
    };

    public static string ToXPath(string jsonPath)
    {
        if (jsonPath == "$") return "/";

        // "$..name" is a search from the document node, which XPath writes "//name".
        if (jsonPath.StartsWith("$..", StringComparison.Ordinal))
            return "//" + Segments(jsonPath[3..]);

        var body = jsonPath.StartsWith("$.", StringComparison.Ordinal) ? jsonPath[2..] : jsonPath.TrimStart('$');
        return body.Length == 0 ? "/" : "/" + RootName + "/" + Segments(body);
    }

    /// <summary>
    /// Rewrites the segments after the root marker. Array subscripts become item steps, and
    /// XPath positions are 1-based where JSONPath indices are 0-based.
    /// </summary>
    private static string Segments(string body)
    {
        var parts = new List<string>();

        foreach (var raw in body.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var seg = raw;
            while (seg.Length > 0)
            {
                var open = seg.IndexOf('[');
                if (open < 0) { parts.Add(seg); break; }

                var close = seg.IndexOf(']');
                if (open > 0) parts.Add(seg[..open]);

                var subscript = seg[(open + 1)..close];
                parts.Add(subscript == "*"
                    ? ItemName
                    : $"{ItemName}[{int.Parse(subscript, CultureInfo.InvariantCulture) + 1}]");

                seg = seg[(close + 1)..];
            }
        }

        return string.Join("/", parts);
    }

    private static string Text(JToken t) => t.Type switch
    {
        JTokenType.Boolean => (bool)t! ? "true" : "false",
        JTokenType.Float   => ((double)t!).ToString(CultureInfo.InvariantCulture),
        JTokenType.Integer => ((long)t!).ToString(CultureInfo.InvariantCulture),
        _                  => t.ToString()
    };
}
