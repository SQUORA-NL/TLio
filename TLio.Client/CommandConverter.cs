using System.Reflection;
using System.Text.Json;
using TLio.Commands.Advanced;
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

            // Object/array values may contain embedded "=func()" strings — use
            // ExpandingFixedValue so those are evaluated lazily at GetValue time.
            if (element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array)
            {
                try
                {
                    var node = _nodeAdapter.Parse(element.GetRawText());
                    return new ExpandingFixedValue<TNode>(node, _functionConverter, _nodeAdapter);
                }
                catch
                {
                    return null;
                }
            }

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

        if (targetType.IsGenericType &&
            targetType.GetGenericTypeDefinition() == typeof(DecisionTableConfig<>))
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;
            return ParseDecisionTableConfig(element);
        }

        return null;
    }

    // ── DecisionTable config parsing ──────────────────────────────────────────

    private DecisionTableConfig<TNode> ParseDecisionTableConfig(JsonElement element)
    {
        var config = new DecisionTableConfig<TNode>();

        if (element.TryGetProperty("inputs", out var inputs) &&
            inputs.ValueKind == JsonValueKind.Array)
        {
            foreach (var inp in inputs.EnumerateArray())
            {
                var di = new DecisionInput();
                if (inp.TryGetProperty("name", out var n)) di.Name = n.GetString() ?? string.Empty;
                if (inp.TryGetProperty("path", out var p)) di.Path = p.GetString() ?? string.Empty;
                config.Inputs.Add(di);
            }
        }

        if (element.TryGetProperty("outputs", out var outputs) &&
            outputs.ValueKind == JsonValueKind.Array)
        {
            foreach (var out_ in outputs.EnumerateArray())
            {
                var dout = new DecisionOutput();
                if (out_.TryGetProperty("name", out var n)) dout.Name = n.GetString() ?? string.Empty;
                if (out_.TryGetProperty("path", out var p)) dout.Path = p.GetString() ?? string.Empty;
                config.Outputs.Add(dout);
            }
        }

        if (element.TryGetProperty("rules", out var rules) &&
            rules.ValueKind == JsonValueKind.Array)
        {
            foreach (var ruleEl in rules.EnumerateArray())
            {
                var rule = new DecisionRule<TNode>();
                if (ruleEl.TryGetProperty("priority", out var pri) &&
                    pri.TryGetInt32(out var priVal))
                    rule.Priority = priVal;

                if (ruleEl.TryGetProperty("conditions", out var conds) &&
                    conds.ValueKind == JsonValueKind.Object)
                {
                    foreach (var cond in conds.EnumerateObject())
                    {
                        var condNode = _nodeAdapter.Parse(cond.Value.GetRawText());
                        rule.Conditions[cond.Name] = condNode;
                    }
                }

                if (ruleEl.TryGetProperty("results", out var results) &&
                    results.ValueKind == JsonValueKind.Object)
                {
                    foreach (var res in results.EnumerateObject())
                    {
                        var value = ConvertJsonValue(res.Value, typeof(IFunctionSupportedValue<TNode>));
                        if (value is IFunctionSupportedValue<TNode> fsv)
                            rule.Results[res.Name] = fsv;
                    }
                }

                config.Rules.Add(rule);
            }
        }

        if (element.TryGetProperty("strategy", out var strategy) &&
            strategy.ValueKind == JsonValueKind.Object)
        {
            var s = new DecisionTableExecutionStrategy();
            if (strategy.TryGetProperty("mode", out var mode) &&
                mode.ValueKind == JsonValueKind.String)
                s.Mode = mode.GetString() ?? "firstMatch";
            if (strategy.TryGetProperty("conflictResolution", out var cr) &&
                cr.ValueKind == JsonValueKind.String)
                s.ConflictResolution = cr.GetString() ?? "priority";
            config.Strategy = s;
        }

        if (element.TryGetProperty("defaultResults", out var defaults) &&
            defaults.ValueKind == JsonValueKind.Object)
        {
            config.DefaultResults = new Dictionary<string, IFunctionSupportedValue<TNode>>();
            foreach (var def in defaults.EnumerateObject())
            {
                var value = ConvertJsonValue(def.Value, typeof(IFunctionSupportedValue<TNode>));
                if (value is IFunctionSupportedValue<TNode> fsv)
                    config.DefaultResults[def.Name] = fsv;
            }
        }

        return config;
    }

    private static string ToPascalCase(string camelCase)
    {
        if (string.IsNullOrEmpty(camelCase)) return camelCase;
        return char.ToUpperInvariant(camelCase[0]) + camelCase.Substring(1);
    }
}
