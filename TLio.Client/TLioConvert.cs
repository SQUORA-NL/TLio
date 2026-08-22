using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TLio.Commands;
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

            var key = KeyFor(command, prop);

            switch (value)
            {
                // An unset string is absent, not "" — that is what keeps an undocumented
                // command from serializing a title and a description.
                case string s when string.IsNullOrEmpty(s):
                    continue;

                // Merge settings are only emitted when they deviate from the default,
                // so scripts that never used them serialize exactly as before.
                case MergeSettings { IsDefault: true }:
                    continue;

                case ArrayMergeMode m:
                    writer.WriteString(key, m.ToString().ToLowerInvariant());
                    continue;

                case MergeSettings mergeSettings:
                    writer.WritePropertyName(key);
                    writer.WriteRawValue(JsonSerializer.Serialize(mergeSettings, MergeSettingsSerializerOptions));
                    continue;

                case IFunctionSupportedValue<TNode> fsv:
                {
                    // A value that cannot be rendered is left out rather than written as null:
                    // an absent value and a null one mean different things to a command.
                    var json = SerializeFsv(fsv, adapter);
                    if (json == null) continue;
                    writer.WritePropertyName(key);
                    writer.WriteRawValue(json);
                    continue;
                }

                case TLioScript<TNode> sub:
                    writer.WritePropertyName(key);
                    writer.WriteRawValue(Serialize(sub, adapter));
                    continue;
            }

            writer.WritePropertyName(key);
            WriteValue(writer, value, adapter);
        }

        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// The field name a property is written under: the camelCase spelling, with the one alias
    /// the JSON notation carries — a decision table's configuration is the <c>Config</c>
    /// property but is written as <c>decisionTable</c>, which is what
    /// <see cref="CommandConverter{TNode}"/> reads and what every script and sample spells.
    /// </summary>
    private static string KeyFor<TNode>(ICommand<TNode> command, PropertyInfo prop)
    {
        if (prop.Name == "Config" && command is DecisionTable<TNode>)
            return "decisionTable";

        return char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
    }

    /// <summary>
    /// Writes any value a command property can hold.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reflective rather than type-by-type, for the same reason
    /// <see cref="CommandConverter{TNode}"/> reads settings reflectively: the settings types
    /// live in packages this one must not reference. <c>DecisionTableConfig</c> is generic over
    /// the node type, the <c>ResolveSetting</c> list lives in TLio.Extensions.ETL, and both have
    /// to survive a round trip all the same.
    /// </para>
    /// <para>
    /// Until this existed the writer knew six shapes and dropped everything else without a word.
    /// A decision table, a resolve, a compare or a csv setting went out as a command with only
    /// its name and path, and running what came back said "Config property is required" — a
    /// script that was correct when written and empty by the time it ran.
    /// </para>
    /// </remarks>
    private static void WriteValue<TNode>(Utf8JsonWriter writer, object? value, INodeAdapter<TNode> adapter)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;

            // Both of these are checked before the collection cases: a node is a tree the
            // adapter renders, and for most adapters it is also an IEnumerable, which would
            // otherwise flatten it into an array of its children.
            case IFunctionSupportedValue<TNode> fsv:
                writer.WriteRawValue(SerializeFsv(fsv, adapter) ?? "null");
                return;

            case TNode node:
                WriteNode(writer, node, adapter);
                return;

            case TLioScript<TNode> script:
                writer.WriteRawValue(Serialize(script, adapter));
                return;

            case string s:
                writer.WriteStringValue(s);
                return;

            case bool b:
                writer.WriteBooleanValue(b);
                return;

            // camelCase, matching the JsonStringEnumConverter the parser reads them with.
            case Enum e:
                writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(e.ToString()));
                return;

            case byte or sbyte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value));
                return;

            case ulong u:
                writer.WriteNumberValue(u);
                return;

            case decimal m:
                writer.WriteNumberValue(m);
                return;

            case float or double:
                writer.WriteNumberValue(Convert.ToDouble(value));
                return;

            case IDictionary dictionary:
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(entry.Key.ToString() ?? string.Empty);
                    WriteValue(writer, entry.Value, adapter);
                }
                writer.WriteEndObject();
                return;

            case IEnumerable items:
                writer.WriteStartArray();
                foreach (var item in items) WriteValue(writer, item, adapter);
                writer.WriteEndArray();
                return;
        }

        // Anything else is a settings object: its readable properties, camelCased — the shape the
        // POCO fallback in CommandConverter deserializes.
        //
        // Nulls are written rather than skipped, which is the opposite of the rule for a command
        // property. Inside a settings object an absent field means "use the default", and the
        // defaults are not null: flatten's metadataPath defaults to "$", so a settings object
        // that had cleared it came back with metadata switched on. A written null reads back as
        // null and the setting keeps the value it was given.
        writer.WriteStartObject();
        foreach (var prop in value.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            writer.WritePropertyName(char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..]);
            WriteValue(writer, prop.GetValue(value), adapter);
        }
        writer.WriteEndObject();
    }

    private static string? SerializeFsv<TNode>(IFunctionSupportedValue<TNode> fsv, INodeAdapter<TNode> adapter)
    {
        // A value parsed from a script is written as the expression it was parsed from, when
        // that expression is a string — the quotes in '$.name' are what make it a literal
        // rather than a path, and the node alone does not remember them. Written from the node,
        // the value came back as a path, resolved, and the script quietly meant something else.
        // Numbers, booleans and structures carry no such ambiguity and are written as
        // themselves, which is also what keeps 1.0 from collapsing to 1.
        if (fsv is FixedValue<TNode> { ScriptText: { } asWritten } fixedValue &&
            adapter.GetNodeKind(fixedValue.Node) == NodeKind.String)
            return JsonSerializer.Serialize(asWritten);

        // A value that holds a node is written from the node.
        if (NodeOf(fsv) is TNode node)
            return RenderNode(node, adapter);

        // Function-expression value (e.g., FunctionSupportedValue): ToScript() returns "=func(args)",
        // and a PathValue returns the path. The two placeholders are not values and are not written.
        var script = fsv.ToScript();
        if (!string.IsNullOrEmpty(script) && script != "[fixed]" && script != "[expanding]")
            return JsonSerializer.Serialize(script);

        return null;
    }

    /// <summary>The node a value holds, if it holds one rather than computing one.</summary>
    /// <remarks>
    /// <see cref="ExpandingFixedValue{TNode}"/> is the object-or-array case: its template is the
    /// value as written, with any "=func()" inside it still unevaluated. It used to fall through
    /// to <see cref="IFunctionSupportedValue{TNode}.ToScript"/>, which answers "[expanding]" —
    /// so every structured value serialized to that word.
    /// </remarks>
    private static TNode? NodeOf<TNode>(IFunctionSupportedValue<TNode> fsv) => fsv switch
    {
        FixedValue<TNode> fixedValue          => fixedValue.Node,
        ExpandingFixedValue<TNode> expanding  => expanding.Template,
        // Anything else holding a node in the conventional field, so a value type from outside
        // this assembly still round-trips.
        _ => fsv.GetType().GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)
                 ?.GetValue(fsv) is TNode node ? node : default
    };

    /// <summary>Renders a node as JSON.</summary>
    /// <remarks>
    /// Through the adapter's shape API rather than <see cref="INodeAdapter{TNode}.Serialize"/>,
    /// because this is the JSON notation and Serialize writes the format of the *data*: an XML
    /// adapter produced "&lt;value&gt;…" and a YAML one a bare "=concat(…)", neither of which is
    /// JSON. The notation a script is written in is independent of the format it transforms, in
    /// this direction as much as the other.
    /// </remarks>
    private static string RenderNode<TNode>(TNode node, INodeAdapter<TNode> adapter)
    {
        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms);
        WriteNode(writer, node, adapter);
        writer.Flush();
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static void WriteNode<TNode>(Utf8JsonWriter writer, TNode? node, INodeAdapter<TNode> adapter)
    {
        if (node is null || adapter.IsNull(node))
        {
            writer.WriteNullValue();
            return;
        }

        if (adapter.IsObject(node))
        {
            writer.WriteStartObject();
            foreach (var name in adapter.GetPropertyNames(node))
            {
                writer.WritePropertyName(name);
                WriteNode(writer, adapter.GetProperty(node, name), adapter);
            }
            writer.WriteEndObject();
            return;
        }

        if (adapter.IsArray(node))
        {
            writer.WriteStartArray();
            foreach (var item in adapter.GetArrayElements(node))
                WriteNode(writer, item, adapter);
            writer.WriteEndArray();
            return;
        }

        var value = adapter.GetValue(node);
        switch (value)
        {
            case null:   writer.WriteNullValue(); break;
            case bool b: writer.WriteBooleanValue(b); break;
            case string s: writer.WriteStringValue(s); break;

            case sbyte or byte or short or ushort or int or uint or long:
                WriteNumber(writer, node, adapter, () => writer.WriteNumberValue(Convert.ToInt64(value)));
                break;

            case decimal m:
                WriteNumber(writer, node, adapter, () => writer.WriteNumberValue(m));
                break;

            case float or double:
                WriteNumber(writer, node, adapter, () => writer.WriteNumberValue(Convert.ToDouble(value)));
                break;

            // A date, a guid, anything an adapter hands back as itself: written as text, which
            // is how it was read.
            default: writer.WriteStringValue(value.ToString()); break;
        }
    }

    /// <summary>
    /// Writes a number, preferring the adapter's own rendering of it when that rendering is a
    /// JSON number.
    /// </summary>
    /// <remarks>
    /// A CLR round trip loses the difference between <c>1</c> and <c>1.0</c> — both are the
    /// double 1, and <see cref="Utf8JsonWriter"/> writes "1". A rate table written 1.0 came back
    /// 1, and every product of it lost its decimal. The adapter kept the distinction, so it is
    /// asked first; the answer is used only when it is a JSON number, which is a rendering
    /// question and cannot change the value's type.
    /// </remarks>
    private static void WriteNumber<TNode>(
        Utf8JsonWriter writer, TNode node, INodeAdapter<TNode> adapter, Action writeFromClrValue)
    {
        string rendered;
        try { rendered = adapter.Serialize(node, false).Trim(); }
        catch { writeFromClrValue(); return; }

        try
        {
            using var document = JsonDocument.Parse(rendered);
            if (document.RootElement.ValueKind == JsonValueKind.Number)
            {
                writer.WriteRawValue(rendered);
                return;
            }
        }
        catch { /* not JSON at all — an XML fragment, a YAML scalar */ }

        writeFromClrValue();
    }
}
