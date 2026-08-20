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
public class XmlScriptParser<TNode>
{
    private readonly ICommandsProvider<TNode> _commandsProvider;
    private readonly FunctionConverter<TNode> _functionConverter;
    private readonly INodeAdapter<TNode> _nodeAdapter;
    private readonly CommandConverter<TNode> _settingsConverter;

    public XmlScriptParser(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter)
    {
        _commandsProvider   = commandsProvider;
        _functionConverter  = new FunctionConverter<TNode>(functionsProvider);
        _nodeAdapter        = nodeAdapter;
        _settingsConverter  = new CommandConverter<TNode>(commandsProvider, functionsProvider, nodeAdapter);
    }

    public TLioScript<TNode> ParseScript(string xmlText)
    {
        var script = new TLioScript<TNode>();
        XElement root;
        try { root = XElement.Parse(xmlText); }
        catch { return script; }

        foreach (var el in root.Elements())
        {
            var cmd = ParseCommand(el);
            if (cmd != null) script.Add(cmd);
        }
        return script;
    }

    private ICommand<TNode>? ParseCommand(XElement el)
    {
        var commandName = el.Name.LocalName.ToLowerInvariant();
        var command = _commandsProvider.GetCommand(commandName);
        if (command == null)
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
                var node = NodeFromContent(valueContent);
                if (node != null) valueProp.SetValue(command, new FixedValue<TNode>(node));
            }
            else if (!el.HasElements && el.Attribute("value") == null)
            {
                // Text content is the value. An element with neither text nor children carries
                // no value at all, which is different from an empty one — leave it unset so
                // command validation reports it rather than silently writing "".
                var text = el.Value;
                if (!string.IsNullOrEmpty(text))
                {
                    var fsv = _functionConverter.ParseValue(text, _nodeAdapter);
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
            return _functionConverter.ParseValue(raw, _nodeAdapter);

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
            {
                // The wrapper itself is the value node: its children are the properties of an
                // object, or the items of an array. Taking the children instead would turn
                // <value><x>1</x></value> into the bare scalar 1.
                var node = ParseNode(el);
                return node == null ? null : new FixedValue<TNode>(node);
            }
            // <value/> is an empty element, which is how this format writes null.
            return string.IsNullOrEmpty(el.Value)
                ? new FixedValue<TNode>(_nodeAdapter.CreateNull())
                : _functionConverter.ParseValue(el.Value, _nodeAdapter);
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

    /// <summary>
    /// The node that loose child elements describe — the same thing an explicit
    /// <c>&lt;value&gt;</c> wrapper around them would mean, so <c>&lt;set&gt;&lt;a/&gt;&lt;/set&gt;</c>
    /// and <c>&lt;set&gt;&lt;value&gt;&lt;a/&gt;&lt;/value&gt;&lt;/set&gt;</c> agree.
    /// </summary>
    private TNode? NodeFromContent(List<XElement> children) =>
        ParseNode(new XElement("value", children.Select(c => new XElement(c))));

    private TNode? ParseNode(XElement wrapper)
    {
        try { return _nodeAdapter.Parse(wrapper.ToString(SaveOptions.DisableFormatting)); }
        catch { return default; }
    }

    // ── XML → JSON for settings objects ──────────────────────────────────────
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
