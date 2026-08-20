using System.Reflection;
using System.Text;
using System.Text.Json;
using TLio.Client;
using TLio.Commands;
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
public class YamlScriptParser<TNode> : IScriptParser<TNode>
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

    public YamlScriptParser(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter)
    {
        _commandsProvider  = commandsProvider;
        _nodeAdapter       = nodeAdapter;
        _settingsConverter = new CommandConverter<TNode>(commandsProvider, functionsProvider, nodeAdapter);
    }

    /// <inheritdoc />
    public ScriptFormat Format => ScriptFormat.Yaml;

    public TLioScript<TNode> ParseScript(string yamlText)
    {
        var script = new TLioScript<TNode>();
        _parseWarnings.Clear();

        var yamlStream = new YamlStream();
        try { yamlStream.Load(new StringReader(yamlText)); }
        catch (YamlDotNet.Core.YamlException ex)
        {
            // Malformed script text yields an empty script, as it does in every notation. The
            // reason travels with the script so the run reports it instead of silently doing
            // nothing at all.
            script.ParseWarnings.Add($"Script is not well-formed YAML: {ex.Message}");
            return script;
        }

        if (!yamlStream.Documents.Any()) return script;

        var root = yamlStream.Documents[0].RootNode;
        if (root is not YamlSequenceNode sequence)
        {
            script.ParseWarnings.Add("A YAML script must be a sequence of command mappings.");
            return script;
        }

        foreach (var item in sequence.Children)
        {
            if (item is not YamlMappingNode mapping) continue;
            var cmd = ParseCommand(mapping);
            if (cmd != null) script.Add(cmd);
        }

        script.ParseWarnings.AddRange(_parseWarnings);
        return script;
    }

    private ICommand<TNode>? ParseCommand(YamlMappingNode mapping)
    {
        var commandName = GetScalarValue(mapping, "command");
        if (string.IsNullOrWhiteSpace(commandName))
        {
            _parseWarnings.Add("A script entry has no 'command' key and was skipped.");
            return null;
        }

        var command = _commandsProvider.GetCommand(commandName);
        if (command == null)
            return new NotFoundCommand<TNode>(commandName);

        var commandType = command.GetType();

        foreach (var (key, value) in mapping.Children)
        {
            var keyStr = (key as YamlScalarNode)?.Value ?? string.Empty;
            if (keyStr.Equals("command", StringComparison.OrdinalIgnoreCase)) continue;

            var propName = ToPascalCase(keyStr);
            // The one alias the JSON notation also carries: a decision table's configuration is
            // written under "decisionTable" but the property is called Config.
            if (propName == "DecisionTable" && commandType.IsGenericType &&
                commandType.GetGenericTypeDefinition() == typeof(DecisionTable<>))
                propName = "Config";

            var prop = commandType.GetProperty(propName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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
                return _settingsConverter.ParseTextValue(scalar.Value ?? string.Empty, _parseWarnings.Add);

            // A mapping or a sequence is a structured value. It goes out as JSON and comes back
            // through the JSON converter rather than being handed to the adapter as YAML text.
            // The adapter's format is the format of the *data*, which need not be the notation
            // the script is written in — a YAML script transforming an XML document handed the
            // XML adapter YAML text and it threw, and the swallowed failure left the property
            // unset so the command wrote nothing without saying so. The JSON route also picks up
            // the number and boolean typing, and the lazy expansion of an "=func()" nested
            // inside the value.
            if (node is YamlMappingNode or YamlSequenceNode)
                return _settingsConverter.ConvertSettingsFragment(YamlToJson(node), targetType);

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

        // Settings are described by the same field names in every notation, so the node is
        // rendered as JSON and built by the JSON converter — which is the only place that knows
        // how to make the generic ones (DecisionTableConfig, the ResolveSetting list).
        return _settingsConverter.ConvertSettingsFragment(YamlToJson(node), targetType);
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

    /// <summary>
    /// The scalar a key names, matched the way the rest of the parser matches keys: without
    /// regard to case. An exact-key lookup dropped <c>Command: set</c> on the floor — the entry
    /// vanished from the script with no command and no diagnostic.
    /// </summary>
    private static string? GetScalarValue(YamlMappingNode mapping, string key)
    {
        foreach (var (k, v) in mapping.Children)
        {
            if (k is YamlScalarNode { Value: not null } scalarKey &&
                scalarKey.Value!.Equals(key, StringComparison.OrdinalIgnoreCase))
                return (v as YamlScalarNode)?.Value;
        }
        return null;
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
