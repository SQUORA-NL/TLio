using System.Reflection;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using YamlDotNet.RepresentationModel;

namespace TLio.Yaml;

/// <summary>
/// Parses YAML-formatted TLio scripts into TLioScript&lt;TNode&gt;.
///
/// Script format (mirrors the JSON array format):
/// <code>
/// - command: set
///   path: $.name
///   value: Alice
/// - command: remove
///   path: $.obsolete
/// - command: copy
///   fromPath: $.src
///   toPath: $.dst
/// </code>
/// </summary>
public class YamlScriptParser<TNode>
{
    private readonly ICommandsProvider<TNode> _commandsProvider;
    private readonly FunctionConverter<TNode> _functionConverter;
    private readonly INodeAdapter<TNode> _nodeAdapter;

    public YamlScriptParser(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter)
    {
        _commandsProvider  = commandsProvider;
        _functionConverter = new FunctionConverter<TNode>(functionsProvider);
        _nodeAdapter       = nodeAdapter;
    }

    public TLioScript<TNode> ParseScript(string yamlText)
    {
        var script = new TLioScript<TNode>();
        var yamlStream = new YamlStream();
        try { yamlStream.Load(new StringReader(yamlText)); }
        catch { return script; }

        if (!yamlStream.Documents.Any()) return script;

        var root = yamlStream.Documents[0].RootNode;
        if (root is not YamlSequenceNode sequence) return script;

        foreach (var item in sequence.Children)
        {
            if (item is not YamlMappingNode mapping) continue;
            var cmd = ParseCommand(mapping);
            if (cmd != null) script.Add(cmd);
        }
        return script;
    }

    private ICommand<TNode>? ParseCommand(YamlMappingNode mapping)
    {
        var commandName = GetScalarValue(mapping, "command");
        if (commandName == null) return null;

        var command = _commandsProvider.GetCommand(commandName);
        if (command == null)
            return new NotFoundCommand<TNode>(commandName);

        var commandType = command.GetType();

        foreach (var (key, value) in mapping.Children)
        {
            var keyStr = (key as YamlScalarNode)?.Value ?? string.Empty;
            if (keyStr.Equals("command", StringComparison.OrdinalIgnoreCase)) continue;

            var propName = ToPascalCase(keyStr);
            var prop = commandType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite) continue;

            var converted = ConvertValue(value, prop.PropertyType);
            if (converted != null) prop.SetValue(command, converted);
        }

        return command;
    }

    private object? ConvertValue(YamlNode node, Type targetType)
    {
        var scalar = node as YamlScalarNode;

        if (targetType == typeof(string))
            return scalar?.Value ?? node.ToString();

        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            if (bool.TryParse(scalar?.Value, out var b)) return b;
            return null;
        }

        if (targetType == typeof(IFunctionSupportedValue<TNode>))
        {
            if (scalar != null)
                return _functionConverter.ParseValue(scalar.Value ?? string.Empty, _nodeAdapter);
            // Complex YAML node → create as a value via serialization
            if (node is YamlMappingNode or YamlSequenceNode)
            {
                try
                {
                    var stream = new YamlStream(new YamlDocument(node));
                    using var writer = new StringWriter();
                    stream.Save(writer, assignAnchors: false);
                    var yamlStr = writer.ToString();
                    var parsed = _nodeAdapter.Parse(yamlStr);
                    return new FixedValue<TNode>(parsed);
                }
                catch { return null; }
            }
            return null;
        }

        if (targetType == typeof(TLioScript<TNode>) && node is YamlSequenceNode subSeq)
        {
            var sub = new TLioScript<TNode>();
            foreach (var item in subSeq.Children)
            {
                if (item is YamlMappingNode m)
                {
                    var cmd = ParseCommand(m);
                    if (cmd != null) sub.Add(cmd);
                }
            }
            return sub;
        }

        return null;
    }

    private static string? GetScalarValue(YamlMappingNode mapping, string key)
    {
        if (mapping.Children.TryGetValue(new YamlScalarNode(key), out var val))
            return (val as YamlScalarNode)?.Value;
        return null;
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
