using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
///   destinationAsArray: true
/// - command: ifElse
///   condition: "=equals($.n, 5)"
///   ifScript:
///     - command: put
///       path: $.r
///       value: yes-branch
/// </code>
///
/// The property set is the same one the JSON notation accepts — the two notations describe
/// the same commands, so a script that works against JSON has a YAML spelling that does the
/// same thing. See <see cref="CommandConverter{TNode}"/> for the JSON side.
/// </summary>
public class YamlScriptParser<TNode>
{
    /// <summary>
    /// Options for the settings-object fallback, matching <see cref="CommandConverter{TNode}"/>
    /// so the same setting is spelled the same way in both notations.
    /// </summary>
    private static readonly JsonSerializerOptions PocoOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

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

    /// <summary>
    /// True for a YAML null: the plain scalars <c>null</c>, <c>Null</c>, <c>NULL</c>, <c>~</c>
    /// and the empty scalar. Quoting any of them (<c>'null'</c>) makes it the string it looks
    /// like, which is why the scalar style is part of the test.
    /// </summary>
    private static bool IsYamlNull(YamlScalarNode scalar) =>
        scalar.Style is YamlDotNet.Core.ScalarStyle.Plain or YamlDotNet.Core.ScalarStyle.Any &&
        scalar.Value is null or "" or "~" or "null" or "Null" or "NULL";

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
            // A YAML null is a null value, not the four-letter string that spells it.
            if (scalar != null && IsYamlNull(scalar))
                return new FixedValue<TNode>(_nodeAdapter.CreateNull());

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

        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (enumType.IsEnum && scalar?.Value != null)
            return Enum.TryParse(enumType, scalar.Value, ignoreCase: true, out var e) ? e : null;

        // Settings objects and other POCOs are described by the same field names the JSON
        // notation uses, so the node is converted to JSON and deserialised the same way.
        if (targetType.IsClass && !targetType.IsAbstract && !targetType.IsGenericType)
        {
            try { return JsonSerializer.Deserialize(YamlToJson(node), targetType, PocoOptions); }
            catch { return null; }
        }

        return null;
    }

    // ── YAML → JSON for settings objects ─────────────────────────────────────

    private static string YamlToJson(YamlNode node)
    {
        var sb = new StringBuilder();
        WriteJson(node, sb);
        return sb.ToString();
    }

    private static void WriteJson(YamlNode node, StringBuilder sb)
    {
        switch (node)
        {
            case YamlMappingNode map:
                sb.Append('{');
                var firstKey = true;
                foreach (var (k, v) in map.Children)
                {
                    if (!firstKey) sb.Append(',');
                    firstKey = false;
                    sb.Append(JsonSerializer.Serialize((k as YamlScalarNode)?.Value ?? k.ToString()))
                      .Append(':');
                    WriteJson(v, sb);
                }
                sb.Append('}');
                break;

            case YamlSequenceNode seq:
                sb.Append('[');
                for (var i = 0; i < seq.Children.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteJson(seq.Children[i], sb);
                }
                sb.Append(']');
                break;

            case YamlScalarNode scalar:
                if (IsYamlNull(scalar)) { sb.Append("null"); break; }
                var text = scalar.Value!;
                if (scalar.Style is YamlDotNet.Core.ScalarStyle.Plain or YamlDotNet.Core.ScalarStyle.Any)
                {
                    if (bool.TryParse(text, out var b)) { sb.Append(b ? "true" : "false"); break; }
                    if (double.TryParse(text, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out _))
                    { sb.Append(text); break; }
                }
                sb.Append(JsonSerializer.Serialize(text));
                break;

            default:
                sb.Append("null");
                break;
        }
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
