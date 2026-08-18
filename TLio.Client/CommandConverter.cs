using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TLio.Commands;
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
    /// <summary>
    /// Options for the generic POCO settings fallback. Enum members are accepted as
    /// camelCase strings ("valueDifference") as well as their PascalCase and numeric forms.
    /// </summary>
    private static readonly JsonSerializerOptions PocoOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: true) }
    };

    private readonly ICommandsProvider<TNode> _commandsProvider;
    private readonly FunctionConverter<TNode> _functionConverter;
    private readonly INodeAdapter<TNode> _nodeAdapter;

    /// <summary>
    /// Notation warnings raised by <see cref="FunctionConverter{TNode}"/> while parsing.
    /// Parsing has no execution context, so they are handed to the parsed
    /// <see cref="TLioScript{TNode}"/> and logged when it executes.
    /// </summary>
    private readonly List<string> _parseWarnings = new();

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
        _parseWarnings.Clear();

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

        script.ParseWarnings.AddRange(_parseWarnings);
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
            // T003: JLio compatibility — "decisionTable" JSON key maps to the Config property
            if (propName == "DecisionTable" && command is DecisionTable<TNode>)
                propName = "Config";
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
                return _functionConverter.ParseValue(
                    element.GetString() ?? string.Empty, _nodeAdapter, _parseWarnings.Add);

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
                // Integral literals go through CreateValue so they stay integers —
                // CreateNumber(double) would turn "value": 152 into 152.0.
                JsonValueKind.Number when element.TryGetInt64(out var l)
                    => new FixedValue<TNode>(_nodeAdapter.CreateValue(l)),
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

        // T004: List<ResolveSetting<TNode>> — parsed reflectively (no direct reference to TLio.Extensions.ETL)
        if (targetType.IsGenericType &&
            targetType.GetGenericTypeDefinition() == typeof(List<>) &&
            element.ValueKind == JsonValueKind.Array)
        {
            var elementType = targetType.GenericTypeArguments[0];
            if (elementType.IsGenericType &&
                elementType.GetGenericTypeDefinition().Name == "ResolveSetting`1")
                return ParseResolveSettings(element, targetType, elementType);
        }

        // T002: Generic POCO fallback for non-generic, non-abstract class types
        // (e.g., FlattenSettings, CsvSettings, RestoreSettings from TLio.Extensions.ETL,
        //  CompareSettings from TLio.Commands.Advanced)
        if (targetType.IsClass && !targetType.IsAbstract && !targetType.IsGenericType)
        {
            try
            {
                return JsonSerializer.Deserialize(element.GetRawText(), targetType, PocoOptions);
            }
            catch { return null; }
        }

        return null;
    }

    // ── ResolveSetting list parsing (reflective — avoids hard ETL dependency) ─

    private object ParseResolveSettings(JsonElement element, Type listType, Type settingType)
    {
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        var resolveKeysProp = settingType.GetProperty("ResolveKeys");
        var refCollPathProp = settingType.GetProperty("ReferencesCollectionPath");
        var valuesProp      = settingType.GetProperty("Values");
        var resolveKeyType  = resolveKeysProp?.PropertyType.GenericTypeArguments.FirstOrDefault();
        var resolveValType  = valuesProp?.PropertyType.GenericTypeArguments.FirstOrDefault();

        foreach (var itemEl in element.EnumerateArray())
        {
            var setting = Activator.CreateInstance(settingType)!;

            if (itemEl.TryGetProperty("resolveKeys", out var keysEl) &&
                keysEl.ValueKind == JsonValueKind.Array && resolveKeyType != null)
            {
                var keyList = (System.Collections.IList)Activator.CreateInstance(resolveKeysProp!.PropertyType)!;
                var keyPathProp    = resolveKeyType.GetProperty("KeyPath");
                var refKeyPathProp = resolveKeyType.GetProperty("ReferenceKeyPath");
                foreach (var keyEl in keysEl.EnumerateArray())
                {
                    var key = Activator.CreateInstance(resolveKeyType)!;
                    if (keyEl.TryGetProperty("keyPath", out var kp))
                        keyPathProp?.SetValue(key, kp.GetString());
                    if (keyEl.TryGetProperty("referenceKeyPath", out var rkp))
                        refKeyPathProp?.SetValue(key, rkp.GetString());
                    keyList.Add(key);
                }
                resolveKeysProp!.SetValue(setting, keyList);
            }

            if (itemEl.TryGetProperty("referencesCollectionPath", out var rcpEl))
                refCollPathProp?.SetValue(setting, rcpEl.GetString() ?? string.Empty);

            if (itemEl.TryGetProperty("values", out var valuesEl) &&
                valuesEl.ValueKind == JsonValueKind.Array && resolveValType != null)
            {
                var valList    = (System.Collections.IList)Activator.CreateInstance(valuesProp!.PropertyType)!;
                var tpProp     = resolveValType.GetProperty("TargetPath");
                var valProp    = resolveValType.GetProperty("Value");
                var behProp    = resolveValType.GetProperty("ResolveTypeBehavior");

                foreach (var valEl in valuesEl.EnumerateArray())
                {
                    var rv = Activator.CreateInstance(resolveValType)!;
                    if (valEl.TryGetProperty("targetPath", out var tp))
                        tpProp?.SetValue(rv, tp.GetString());
                    if (valEl.TryGetProperty("value", out var vEl))
                        valProp?.SetValue(rv, ConvertJsonValue(vEl, typeof(IFunctionSupportedValue<TNode>)));
                    if (valEl.TryGetProperty("resolveTypeBehavior", out var behEl) &&
                        behEl.ValueKind == JsonValueKind.String && behProp != null)
                    {
                        if (Enum.TryParse(behProp.PropertyType, behEl.GetString(), ignoreCase: true, out var beh))
                            behProp.SetValue(rv, beh);
                    }
                    valList.Add(rv);
                }
                valuesProp!.SetValue(setting, valList);
            }

            list.Add(setting);
        }

        return list;
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
