using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TLio.Client;
using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// Parses XML-formatted TLio scripts into TLioScript&lt;TNode&gt;.
///
/// Script format:
/// <code>
/// &lt;script&gt;
///   &lt;set path="/order/name"&gt;Alice&lt;/set&gt;
///   &lt;add path="/order/address/city"&gt;Amsterdam&lt;/add&gt;
///   &lt;remove path="/order/obsolete"/&gt;
///   &lt;rename path="/order" name="opdracht"/&gt;
///   &lt;copy fromPath="/order/src" toPath="/order/dst" destinationAsArray="true"/&gt;
///   &lt;move fromPath="/order/old" toPath="/order/new"/&gt;
///   &lt;put path="/order/key"&gt;&lt;value&gt;&lt;a&gt;1&lt;/a&gt;&lt;/value&gt;&lt;/put&gt;
///   &lt;ifElse condition="=equals(/order/n, 5)"&gt;
///     &lt;ifScript&gt;&lt;put path="/order/r"&gt;yes&lt;/put&gt;&lt;/ifScript&gt;
///     &lt;elseScript&gt;&lt;put path="/order/r"&gt;no&lt;/put&gt;&lt;/elseScript&gt;
///   &lt;/ifElse&gt;
/// &lt;/script&gt;
/// </code>
///
/// Paths are absolute from the document node, as XPath defines it: <c>/order</c> is the
/// document element and <c>/order/name</c> a child of it. The document element is always
/// named in the path — a bare <c>name</c> is a child of the document node and matches nothing.
///
/// Rules:
/// - Root element name is ignored (it is the script container)
/// - Each child element name = command name (case-insensitive)
/// - Attributes = scalar properties, converted to the target property's type: string, bool,
///   enum, or a value expression (<c>condition="=equals(…)"</c>)
/// - Child elements named after a command property carry the values an attribute cannot hold:
///   a nested script (<c>&lt;ifScript&gt;</c>), a structured value (<c>&lt;value&gt;</c>), or a
///   settings object (<c>&lt;settings&gt;</c>)
/// - Text content = the <c>Value</c> property, same as a <c>value</c> attribute would be
///
/// The property set is the same one the JSON notation accepts — the two notations describe the
/// same commands, so a script that works against JSON has an XML spelling that does the same
/// thing. See <see cref="CommandConverter{TNode}"/> for the JSON side.
/// </summary>
public class XmlScriptParser<TNode> : IScriptParser<TNode>
{
    private readonly ICommandsProvider<TNode> _commandsProvider;
    private readonly INodeAdapter<TNode> _nodeAdapter;
    private readonly CommandConverter<TNode> _settingsConverter;

    /// <summary>
    /// Notation warnings raised by <see cref="FunctionConverter{TNode}"/> while parsing.
    /// Parsing has no execution context, so they are handed to the parsed
    /// <see cref="TLioScript{TNode}"/> and logged when it executes.
    /// </summary>
    private readonly List<string> _parseWarnings = new();

    public XmlScriptParser(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter)
    {
        _commandsProvider   = commandsProvider;
        _nodeAdapter        = nodeAdapter;
        _settingsConverter  = new CommandConverter<TNode>(commandsProvider, functionsProvider, nodeAdapter);
    }

    /// <inheritdoc />
    public ScriptFormat Format => ScriptFormat.Xml;

    public TLioScript<TNode> ParseScript(string xmlText)
    {
        var script = new TLioScript<TNode>();
        _parseWarnings.Clear();

        XElement root;
        try { root = XElement.Parse(xmlText); }
        catch (System.Xml.XmlException ex)
        {
            // Malformed script text yields an empty script, as it does in every notation. The
            // reason travels with the script so the run reports it instead of silently doing
            // nothing at all.
            script.ParseWarnings.Add($"Script is not well-formed XML: {ex.Message}");
            return script;
        }

        foreach (var el in root.Elements())
        {
            var cmd = ParseCommand(el);
            if (cmd != null) script.Add(cmd);
        }

        script.ParseWarnings.AddRange(_parseWarnings);
        return script;
    }

    private ICommand<TNode>? ParseCommand(XElement el)
    {
        var commandName = el.Name.LocalName;
        var command = _commandsProvider.GetCommand(commandName.ToLowerInvariant());
        if (command == null)
            // Reported as the author spelled it: a lowercased "decisionTabel" is harder to
            // recognise as the typo it is.
            return new NotFoundCommand<TNode>(commandName);

        var commandType = command.GetType();

        foreach (var attr in el.Attributes())
        {
            var prop = FindProperty(commandType, attr.Name.LocalName);
            if (prop == null) continue;
            var converted = ConvertScalar(attr.Value, prop.PropertyType);
            if (converted != null) prop.SetValue(command, converted);
        }

        // Child elements name the properties an attribute cannot express. Anything that does
        // not name a property is content for Value — that keeps <set><a>1</a></set> working
        // alongside the explicit <set><value><a>1</a></value></set> form.
        var valueContent = new List<XElement>();
        foreach (var child in el.Elements())
        {
            var prop = FindProperty(commandType, child.Name.LocalName);
            if (prop == null) { valueContent.Add(child); continue; }

            var converted = ConvertElement(child, prop.PropertyType);
            if (converted != null) prop.SetValue(command, converted);
        }

        var valueProp = FindProperty(commandType, "value");
        if (valueProp != null && valueProp.PropertyType == typeof(IFunctionSupportedValue<TNode>) &&
            valueProp.GetValue(command) == null)
        {
            if (valueContent.Count > 0)
            {
                var wrapper = new XElement("value", valueContent.Select(c => new XElement(c)));
                var value = ConvertElement(wrapper, valueProp.PropertyType);
                if (value != null) valueProp.SetValue(command, value);
            }
            else if (!el.HasElements && el.Attribute("value") == null)
            {
                // Text content is the value. An element with neither text nor children carries
                // no value at all, which is different from an empty one — leave it unset so
                // command validation reports it rather than silently writing "".
                var text = el.Value;
                if (!string.IsNullOrEmpty(text))
                {
                    var fsv = _settingsConverter.ParseTextValue(text, _parseWarnings.Add);
                    if (fsv != null) valueProp.SetValue(command, fsv);
                }
            }
        }

        return command;
    }

    /// <summary>
    /// The property a script field names. Mostly the PascalCase spelling of the field, with the
    /// one alias the JSON notation also carries: a decision table's configuration is written
    /// under "decisionTable" but the property is called Config.
    /// </summary>
    private static PropertyInfo? FindProperty(Type commandType, string name)
    {
        var propName = ToPascalCase(name);
        if (propName == "DecisionTable" && commandType.IsGenericType &&
            commandType.GetGenericTypeDefinition() == typeof(DecisionTable<>))
            propName = "Config";

        var prop = commandType.GetProperty(propName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop is { CanWrite: true } ? prop : null;
    }

    /// <summary>Converts an attribute value to the property's type.</summary>
    private object? ConvertScalar(string raw, Type targetType)
    {
        if (targetType == typeof(string))
            return raw;

        if (targetType == typeof(bool) || targetType == typeof(bool?))
            return bool.TryParse(raw, out var b) ? b : null;

        if (targetType == typeof(IFunctionSupportedValue<TNode>))
            return _settingsConverter.ParseTextValue(raw, _parseWarnings.Add);

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (enumType.IsEnum)
            return Enum.TryParse(enumType, raw, ignoreCase: true, out var e) ? e : null;

        return null;
    }

    /// <summary>Converts a child element to the property's type.</summary>
    private object? ConvertElement(XElement el, Type targetType)
    {
        if (targetType == typeof(TLioScript<TNode>))
        {
            var sub = new TLioScript<TNode>();
            foreach (var item in el.Elements())
            {
                var cmd = ParseCommand(item);
                if (cmd != null) sub.Add(cmd);
            }
            return sub;
        }

        if (targetType == typeof(IFunctionSupportedValue<TNode>))
        {
            if (el.HasElements)
                // The wrapper itself is the value node: its children are the properties of an
                // object, or the items of an array. Taking the children instead would turn
                // <value><x>1</x></value> into the bare scalar 1.
                //
                // It goes out as JSON and comes back through the JSON converter rather than
                // being handed to the adapter as XML text. The adapter's format is the format of
                // the *data*, which need not be the notation the script is written in — an XML
                // script transforming a JSON document handed the JSON adapter "<value>…" and it
                // threw, and the swallowed failure left the property unset so the command wrote
                // nothing without saying so. The JSON route also picks up the number and boolean
                // typing, and the lazy expansion of an "=func()" nested inside the value.
                return _settingsConverter.ConvertSettingsFragment(XmlToJson(el), targetType);

            // <value/> is an empty element, which is how this format writes null.
            return string.IsNullOrEmpty(el.Value)
                ? new FixedValue<TNode>(_nodeAdapter.CreateNull())
                : _settingsConverter.ParseTextValue(el.Value, _parseWarnings.Add);
        }

        if (targetType == typeof(string))
            return el.Value;

        if (targetType == typeof(bool) || targetType == typeof(bool?))
            return bool.TryParse(el.Value, out var b) ? b : null;

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (enumType.IsEnum)
            return Enum.TryParse(enumType, el.Value, ignoreCase: true, out var e) ? e : null;

        // Settings are described by the same field names in every notation, so the element is
        // rendered as JSON and built by the JSON converter — which is the only place that knows
        // how to make the generic ones (DecisionTableConfig, the ResolveSetting list).
        return _settingsConverter.ConvertSettingsFragment(XmlToJson(el), targetType);
    }

    // ── XML → JSON for structured values and settings objects ────────────────
    // Uses the same shape the node adapter reads documents in: repeated same-named children
    // (or children named "item") are an array, other children are properties, text is a scalar.

    private static string XmlToJson(XElement el)
    {
        var sb = new StringBuilder();
        WriteJson(el, sb);
        return sb.ToString();
    }

    private static void WriteJson(XElement el, StringBuilder sb)
    {
        if (!el.HasElements)
        {
            var text = el.Value;
            if (text.Length == 0) { sb.Append("null"); return; }
            if (bool.TryParse(text, out var b)) { sb.Append(b ? "true" : "false"); return; }
            if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out _))
            { sb.Append(text); return; }
            sb.Append(JsonSerializer.Serialize(text));
            return;
        }

        var children = el.Elements().ToList();
        var names = children.Select(c => c.Name.LocalName).Distinct().ToList();
        var isArray = names.Count == 1 &&
                      (children.Count > 1 || names[0] == XmlNodeAdapter.DefaultItemName);

        if (isArray)
        {
            sb.Append('[');
            for (var i = 0; i < children.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteJson(children[i], sb);
            }
            sb.Append(']');
            return;
        }

        sb.Append('{');
        for (var i = 0; i < children.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(JsonSerializer.Serialize(children[i].Name.LocalName)).Append(':');
            WriteJson(children[i], sb);
        }
        sb.Append('}');
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
