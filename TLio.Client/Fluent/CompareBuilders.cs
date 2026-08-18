using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Models;

namespace TLio.Client;

// ── Compare builders (JLio-style With / Using / SetResultOn chain) ────────────

/// <summary>Returned by Compare(firstPath). Call .With(secondPath).</summary>
public sealed class CompareWithBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly string _firstPath;

    internal CompareWithBuilder(TLioScript<TNode> script, string firstPath)
    {
        _script = script;
        _firstPath = firstPath;
    }

    public CompareSettingsBuilder<TNode> With(string secondPath)
        => new(_script, _firstPath, secondPath);
}

/// <summary>Call .Using(settings) — optional — then .SetResultOn(path).</summary>
public sealed class CompareSettingsBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly string _firstPath;
    private readonly string _secondPath;
    private CompareSettings? _settings;

    internal CompareSettingsBuilder(TLioScript<TNode> script, string firstPath, string secondPath)
    {
        _script = script;
        _firstPath = firstPath;
        _secondPath = secondPath;
    }

    /// <summary>Attach diff settings (array key matching, result-type filter).</summary>
    public CompareSettingsBuilder<TNode> Using(CompareSettings settings)
    {
        _settings = settings;
        return this;
    }

    /// <summary>Append the Compare command to the script.</summary>
    public TLioScript<TNode> SetResultOn(string resultPath)
    {
        _script.Add(new Compare<TNode>
        {
            FirstPath  = _firstPath,
            SecondPath = _secondPath,
            ResultPath = resultPath,
            Settings   = _settings,
        });
        return _script;
    }
}

/// <summary>Fluent entry point for the With / Using / SetResultOn Compare chain.</summary>
public static class CompareScriptExtensions
{
    /// <summary>
    /// Start a Compare command. Call .With(secondPath)[.Using(settings)].SetResultOn(resultPath).
    /// </summary>
    public static CompareWithBuilder<TNode> Compare<TNode>(this TLioScript<TNode> script, string firstPath)
        => new(script, firstPath);
}
