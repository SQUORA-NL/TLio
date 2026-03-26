using System.Reflection;
using System.Text.Json;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Deserializes a JSON script array into a TLioScript&lt;TNode&gt; using the "command"
/// field as a discriminator.
///
/// Each JSON array element must be an object with a "command" property naming a
/// registered ICommand. Remaining camelCase properties are mapped to PascalCase
/// C# properties via reflection.
///
/// Supported target property types:
///   string                         ← JSON string (direct)
///   bool                           ← JSON true/false
///   ArrayMergeMode                 ← JSON string (enum parse)
///   IFunctionSupportedValue&lt;TNode&gt; ← JSON string (via FunctionConverter) or literal (FixedValue)
///   TLioScript&lt;TNode&gt;              ← JSON array (recursive)
///
/// Unknown command names yield NotFoundCommand&lt;TNode&gt; (logs warning, no exception).
///
/// Ported from JLio's CommandConverter / ParseContext.
/// </summary>
public class CommandConverter<TNode>
{
    private readonly ICommandsProvider<TNode> _commandsProvider;
    private readonly FunctionConverter<TNode> _functionConverter;
    private readonly INodeAdapter<TNode> _nodeAdapter;

    public CommandConverter(
        ICommandsProvider<TNode> commandsProvider,
        IFunctionsProvider<TNode> functionsProvider,
        INodeAdapter<TNode> nodeAdapter)
    {
        _commandsProvider = commandsProvider;
        _functionConverter = new FunctionConverter<TNode>(functionsProvider);
        _nodeAdapter = nodeAdapter;
    }

    /// <summary>Parse a JSON script string into a TLioScript.</summary>
    public TLioScript<TNode> ParseScript(string scriptJson)
    {
        var script = new TLioScript<TNode>();

        JsonDocument doc;
        try { doc = JsonDocument.Parse(scriptJson); }
        catch { return script; }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return script;

            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var command = ParseCommand(element);
                if (command != null)
                    script.Add(command);
            }
        }

        return script;
    }

    private ICommand<TNode>? ParseCommand(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;

        if (!element.TryGetProperty("command", out var commandProp))
            return null;

        var commandName = commandProp.GetString() ?? string.Empty;
        var command = _commandsProvider.GetCommand(commandName);
        if (command == null)
            return new NotFoundCommand<TNode>(commandName);

        var commandType = command.GetType();
        foreach (var jsonProp in element.EnumerateObject())
        {
            if (jsonProp.Name.Equals("command", StringComparison.OrdinalIgnoreCase))
                continue;

            var propName = ToPascalCase(jsonProp.Name);
            var csProp = commandType.GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
            if (csProp == null || !csProp.CanWrite)
                continue;

            var value = ConvertJsonValue(jsonProp.Value, csProp.PropertyType);
            if (value != null)
                csProp.SetValue(command, value);
        }

        return command;
    }

    private object? ConvertJsonValue(JsonElement element, Type targetType)
    {
        if (targetType == typeof(string))
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.ToString();

        if (targetType == typeof(bool) || targetType == typeof(bool?))
        {
            if (element.ValueKind == JsonValueKind.True) return true;
            if (element.ValueKind == JsonValueKind.False) return false;
            return null;
        }

        if (targetType == typeof(ArrayMergeMode) || targetType == typeof(ArrayMergeMode?))
        {
            if (element.ValueKind == JsonValueKind.String &&
                Enum.TryParse<ArrayMergeMode>(element.GetString(), ignoreCase: true, out var mode))
                return mode;
            return null;
        }

        if (targetType == typeof(IFunctionSupportedValue<TNode>))
        {
            if (element.ValueKind == JsonValueKind.String)
                return _functionConverter.ParseValue(element.GetString() ?? string.Empty, _nodeAdapter);

            return element.ValueKind switch
            {
                JsonValueKind.True  => new FixedValue<TNode>(_nodeAdapter.CreateBoolean(true)),
                JsonValueKind.False => new FixedValue<TNode>(_nodeAdapter.CreateBoolean(false)),
                JsonValueKind.Number when element.TryGetDouble(out var d)
                    => new FixedValue<TNode>(_nodeAdapter.CreateNumber(d)),
                JsonValueKind.Null  => new FixedValue<TNode>(_nodeAdapter.CreateNull()),
                _ => null
            };
        }

        if (targetType == typeof(TLioScript<TNode>))
        {
            if (element.ValueKind != JsonValueKind.Array)
                return null;

            var subScript = new TLioScript<TNode>();
            foreach (var item in element.EnumerateArray())
            {
                var cmd = ParseCommand(item);
                if (cmd != null) subScript.Add(cmd);
            }
            return subScript;
        }

        return null;
    }

    private static string ToPascalCase(string camelCase)
    {
        if (string.IsNullOrEmpty(camelCase)) return camelCase;
        return char.ToUpperInvariant(camelCase[0]) + camelCase.Substring(1);
    }
}
