using System.Reflection;
using System.Text;
using System.Text.Json;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Static helpers for parsing and serializing TLio scripts.
///
/// Parse: delegates to CommandConverter using the supplied ParseOptions + INodeAdapter.
/// Serialize: reflects over command properties to produce a JSON array string.
///            Requires an INodeAdapter to serialize IFunctionSupportedValue nodes.
/// </summary>
public static class TLioConvert
{
    private static readonly JsonSerializerOptions MergeSettingsSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Parse a JSON script string into a TLioScript.
    ///
    /// The XML and YAML notations are read by their own parsers, which ship with their format
    /// package — build one with <c>CreateXmlScriptParser</c> / <c>CreateYamlScriptParser</c>, or
    /// register both on a <see cref="ScriptEngine{TNode}"/> and let it pick.
    /// </summary>
    public static TLioScript<TNode> Parse<TNode>(
        string scriptJson,
        ParseOptions<TNode> options,
        INodeAdapter<TNode> adapter)
    {
        var converter = new CommandConverter<TNode>(
            options.CommandsProvider,
            options.FunctionsProvider,
            adapter);
        return converter.ParseScript(scriptJson);
    }

    /// <summary>
    /// Serialize a TLioScript to a JSON array string.
    /// An INodeAdapter is required to serialize IFunctionSupportedValue property values.
    /// </summary>
    public static string Serialize<TNode>(TLioScript<TNode> script, INodeAdapter<TNode> adapter)
    {
        var sb = new StringBuilder("[");
        bool first = true;
        foreach (var command in script)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(SerializeCommand(command, adapter));
        }
        sb.Append(']');
        return sb.ToString();
    }

    private static string SerializeCommand<TNode>(ICommand<TNode> command, INodeAdapter<TNode> adapter)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);

        writer.WriteStartObject();
        writer.WriteString("command", command.CommandName);

        foreach (var prop in command.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.Name != "CommandName"))
        {
            var value = prop.GetValue(command);
            if (value == null) continue;

            var key = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];

            switch (value)
            {
                case string s when !string.IsNullOrEmpty(s):
                    writer.WriteString(key, s);
                    break;

                case bool b:
                    writer.WriteBoolean(key, b);
                    break;

                case ArrayMergeMode m:
                    writer.WriteString(key, m.ToString().ToLowerInvariant());
                    break;

                // Merge settings are only emitted when they deviate from the default,
                // so scripts that never used them serialize exactly as before.
                case MergeSettings mergeSettings when !mergeSettings.IsDefault:
                    writer.WritePropertyName(key);
                    writer.WriteRawValue(JsonSerializer.Serialize(mergeSettings, MergeSettingsSerializerOptions));
                    break;

                case IFunctionSupportedValue<TNode> fsv:
                {
                    var json = SerializeFsv(fsv, adapter);
                    if (json != null)
                    {
                        writer.WritePropertyName(key);
                        writer.WriteRawValue(json);
                    }
                    break;
                }

                case TLioScript<TNode> sub:
                    writer.WritePropertyName(key);
                    writer.WriteRawValue(Serialize(sub, adapter));
                    break;
            }
        }

        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string? SerializeFsv<TNode>(IFunctionSupportedValue<TNode> fsv, INodeAdapter<TNode> adapter)
    {
        // FixedValue<TNode> holds a TNode in a private field — extract via reflection
        // and serialize with the adapter (e.g., JToken → JSON string like "hello" or 42)
        var valueField = fsv.GetType()
            .GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
        if (valueField?.GetValue(fsv) is TNode node)
            return adapter.Serialize(node, false);

        // Function-expression value (e.g., FunctionSupportedValue): ToScript() returns "=func(args)"
        var script = fsv.ToScript();
        if (!string.IsNullOrEmpty(script) && script != "[fixed]")
            return JsonSerializer.Serialize(script);

        return null;
    }
}
