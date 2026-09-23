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
/// Two of those properties are on every command and configure nothing: "title" and
/// "description" are free text for whoever reads the script next. They are parsed like any
/// other string property and never looked at again.
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
public class CommandConverter<TNode> : IScriptParser<TNode>
{
    /// <inheritdoc />
    public ScriptFormat Format => ScriptFormat.Json;

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
        catch (JsonException ex)
        {
            // Malformed script text yields an empty script rather than an exception — callers
            // are often handing on text someone else wrote. The reason travels with the script
            // so the run can report it instead of silently doing nothing at all.
            script.ParseWarnings.Add($"Script is not well-formed JSON: {ex.Message}");
            return script;
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                script.ParseWarnings.Add("A JSON script must be an array of command objects.");
                return script;
            }

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

    /// <summary>
    /// Converts a JSON fragment into a value for a command property of
    /// <paramref name="targetType"/>.
    ///
    /// The settings a command carries — <c>DecisionTableConfig</c>, the <c>ResolveSetting</c>
    /// list, the plain POCOs — are described by the same field names in every notation, but
    /// only this class knows how to build them: the first two are generic over the node type
    /// and cannot go through a plain deserialiser. The XML and YAML parsers render their own
    /// settings node as JSON and come here, so a setting spelled in one notation means the same
    /// thing spelled in another instead of being silently dropped.
    /// </summary>
    public object? ConvertSettingsFragment(string json, Type targetType)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ConvertJsonValue(document.RootElement, targetType);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a value written as text in another notation — an XML attribute, a YAML scalar —
    /// into the value a command property takes, reporting notation warnings the same way a JSON
    /// string value does.
    /// </summary>
    public IFunctionSupportedValue<TNode>? ParseTextValue(string raw, Action<string>? onWarning = null) =>
        _functionConverter.ParseValue(raw, _nodeAdapter, onWarning);

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
            //
            // Built through the adapter rather than by handing it the raw JSON text: Parse
            // expects a document in the adapter's own format, so an XML adapter was handed
            // {"x":1} and threw. The catch turned that into a value that was never set, and the
            // command wrote nothing without saying so.
            if (element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array)
                return new ExpandingFixedValue<TNode>(NodeFromJson(element), _functionConverter, _nodeAdapter);

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
                var tpExprProp = resolveValType.GetProperty("TargetPathExpression");
                var valProp    = resolveValType.GetProperty("Value");
                var behProp    = resolveValType.GetProperty("ResolveTypeBehavior");

                foreach (var valEl in valuesEl.EnumerateArray())
                {
                    var rv = Activator.CreateInstance(resolveValType)!;
                    if (valEl.TryGetProperty("targetPath", out var tp))
                    {
                        var tpText = tp.GetString();
                        tpProp?.SetValue(rv, tpText);

                        // A targetPath written as a function expression computes the property
                        // name from the matched reference entry instead of naming it literally —
                        // parsed the same way "value" is, kept on a separate property so the
                        // plain "@.property" form (the overwhelming majority of scripts) is
                        // completely unaffected.
                        if (tpExprProp != null && tpText != null &&
                            tpText.StartsWith("=", StringComparison.Ordinal))
                        {
                            var expr = ConvertJsonValue(tp, typeof(IFunctionSupportedValue<TNode>));
                            tpExprProp.SetValue(rv, expr);
                        }
                    }
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

    /// <summary>
    /// Builds a node of the target format from a JSON fragment, through the adapter's own
    /// creation methods.
    ///
    /// The obvious alternative — handing the fragment's raw text to
    /// <see cref="INodeAdapter{TNode}.Parse"/> — only works when the target format is JSON.
    /// A decision table condition of <c>"active"</c> reaches the adapter as the four bytes
    /// <c>"active"</c> including its quotes, which is a document in no other format: the XML
    /// adapter threw on it, so a decision table could not be written in any notation but JSON.
    /// </summary>
    private TNode NodeFromJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var obj = _nodeAdapter.CreateObject();
                foreach (var property in element.EnumerateObject())
                    _nodeAdapter.SetProperty(obj, property.Name, NodeFromJson(property.Value));
                return obj;
            }

            case JsonValueKind.Array:
            {
                var array = _nodeAdapter.CreateArray();
                foreach (var item in element.EnumerateArray())
                    _nodeAdapter.AppendToArray(array, NodeFromJson(item));
                return array;
            }

            case JsonValueKind.String:
                return _nodeAdapter.CreateString(element.GetString() ?? string.Empty);

            case JsonValueKind.True:  return _nodeAdapter.CreateBoolean(true);
            case JsonValueKind.False: return _nodeAdapter.CreateBoolean(false);

            // Integral literals go through CreateValue so they stay integers, matching the
            // rule the value branch above uses.
            case JsonValueKind.Number when element.TryGetInt64(out var l):
                return _nodeAdapter.CreateValue(l);
            case JsonValueKind.Number:
                return _nodeAdapter.CreateNumber(element.GetDouble());

            default:
                return _nodeAdapter.CreateNull();
        }
    }

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
                        rule.Conditions[cond.Name] = NodeFromJson(cond.Value);
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
