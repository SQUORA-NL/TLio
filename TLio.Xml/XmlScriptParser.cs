using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using TLio.Client;
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
///   &lt;copy fromPath="/order/src" toPath="/order/dst"/&gt;
///   &lt;move fromPath="/order/old" toPath="/order/new"/&gt;
///   &lt;put path="/order/key"&gt;value&lt;/put&gt;
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
/// - Attributes = string properties (path, fromPath, toPath, property, ...)
/// - Text content (or first child element) = "Value" property as IFunctionSupportedValue
/// </summary>
public class XmlScriptParser<TNode>
{
    private readonly ICommandsProvider<TNode> _commandsProvider;
    private readonly FunctionConverter<TNode> _functionConverter;
    private readonly INodeAdapter<TNode> _nodeAdapter;

    public XmlScriptParser(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter)
    {
        _commandsProvider   = commandsProvider;
        _functionConverter  = new FunctionConverter<TNode>(functionsProvider);
        _nodeAdapter        = nodeAdapter;
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

        // Map attributes to string properties
        foreach (var attr in el.Attributes())
        {
            var propName = ToPascalCase(attr.Name.LocalName);
            var prop = commandType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) continue;

            if (prop.PropertyType == typeof(string))
                prop.SetValue(command, attr.Value);
        }

        // Text content → Value property
        var valueProp = commandType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
        if (valueProp != null)
        {
            if (!el.HasElements && !string.IsNullOrEmpty(el.Value))
            {
                var fsv = _functionConverter.ParseValue(el.Value, _nodeAdapter);
                if (fsv != null) valueProp.SetValue(command, fsv);
            }
            else if (el.HasElements)
            {
                // First child element is the value node (complex value)
                var first = el.Elements().First();
                try
                {
                    var node = _nodeAdapter.Parse(first.ToString(SaveOptions.DisableFormatting));
                    valueProp.SetValue(command, new FixedValue<TNode>(node));
                }
                catch { /* ignore parse errors */ }
            }
        }

        return command;
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
