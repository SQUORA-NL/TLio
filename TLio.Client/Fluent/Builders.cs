using TLio.Commands;
using TLio.Commands.Advanced;
using TLio.Commands.Advanced.Settings;
using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

// ── Value / Remove builders ───────────────────────────────────────────────────

/// <summary>Returned by Add/Set/Put. Call OnPath(path) to complete the command.</summary>
public sealed class ValueOnPathBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly string _commandType; // "add", "set", or "put"
    private readonly IFunctionSupportedValue<TNode> _value;

    internal ValueOnPathBuilder(TLioScript<TNode> script, string commandType, IFunctionSupportedValue<TNode> value)
    {
        _script = script;
        _commandType = commandType;
        _value = value;
    }

    public TLioScript<TNode> OnPath(string path)
    {
        ICommand<TNode> cmd = _commandType switch
        {
            "set"  => new Set<TNode>  { Path = path, Value = _value },
            "put"  => new Put<TNode>  { Path = path, Value = _value },
            _      => new Add<TNode>  { Path = path, Value = _value },
        };
        _script.Add(cmd);
        return _script;
    }
}

/// <summary>Returned by Remove(). Call OnPath(path) to complete the command.</summary>
public sealed class RemoveOnPathBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;

    internal RemoveOnPathBuilder(TLioScript<TNode> script) => _script = script;

    public TLioScript<TNode> OnPath(string path)
    {
        _script.Add(new Remove<TNode> { Path = path });
        return _script;
    }
}

// ── Copy / Move builders ──────────────────────────────────────────────────────

/// <summary>Returned by Copy()/Move(). Call From(path).</summary>
public sealed class CopyMoveFromBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly bool _isMove;

    internal CopyMoveFromBuilder(TLioScript<TNode> script, bool isMove)
    {
        _script = script;
        _isMove = isMove;
    }

    public CopyMoveToBuilder<TNode> From(string fromPath)
        => new(_script, _isMove, fromPath);
}

/// <summary>Call To(path) to complete the Copy/Move command.</summary>
public sealed class CopyMoveToBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly bool _isMove;
    private readonly string _fromPath;

    internal CopyMoveToBuilder(TLioScript<TNode> script, bool isMove, string fromPath)
    {
        _script = script;
        _isMove = isMove;
        _fromPath = fromPath;
    }

    public TLioScript<TNode> To(string toPath)
    {
        ICommand<TNode> cmd = _isMove
            ? new Move<TNode> { FromPath = _fromPath, ToPath = toPath }
            : new Copy<TNode> { FromPath = _fromPath, ToPath = toPath };
        _script.Add(cmd);
        return _script;
    }
}

// ── Compare builders ──────────────────────────────────────────────────────────

/// <summary>Returned by Compare(). Call From(path).</summary>
public sealed class CompareFromBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;

    internal CompareFromBuilder(TLioScript<TNode> script) => _script = script;

    public CompareToBuilder<TNode> From(string fromPath)
        => new(_script, fromPath);
}

/// <summary>Call To(path).</summary>
public sealed class CompareToBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly string _fromPath;

    internal CompareToBuilder(TLioScript<TNode> script, string fromPath)
    {
        _script = script;
        _fromPath = fromPath;
    }

    public CompareResultBuilder<TNode> To(string toPath)
        => new(_script, _fromPath, toPath);
}

/// <summary>Call Result(path) to complete the Compare command.</summary>
public sealed class CompareResultBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly string _fromPath;
    private readonly string _toPath;
    private CompareSettings? _settings;

    internal CompareResultBuilder(TLioScript<TNode> script, string fromPath, string toPath)
    {
        _script = script;
        _fromPath = fromPath;
        _toPath = toPath;
    }

    /// <summary>Attach diff settings (array key matching, result-type filter).</summary>
    public CompareResultBuilder<TNode> Using(CompareSettings settings)
    {
        _settings = settings;
        return this;
    }

    public TLioScript<TNode> Result(string resultPath)
    {
        _script.Add(new Compare<TNode>
        {
            FirstPath  = _fromPath,
            SecondPath = _toPath,
            ResultPath = resultPath,
            Settings   = _settings
        });
        return _script;
    }
}

// ── Merge builders ────────────────────────────────────────────────────────────

/// <summary>Returned by Merge(). Call From(path).</summary>
public sealed class MergeFromBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;

    internal MergeFromBuilder(TLioScript<TNode> script) => _script = script;

    public MergeToBuilder<TNode> From(string fromPath)
        => new(_script, fromPath);
}

/// <summary>Call To(path) to complete the Merge command.</summary>
public sealed class MergeToBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly string _fromPath;

    internal MergeToBuilder(TLioScript<TNode> script, string fromPath)
    {
        _script = script;
        _fromPath = fromPath;
    }

    public TLioScript<TNode> To(string toPath)
    {
        _script.Add(new Merge<TNode> { Path = _fromPath, TargetPath = toPath });
        return _script;
    }
}

// ── IfElse builders ───────────────────────────────────────────────────────────

/// <summary>Returned by IfElse(condition). Call If(ifScript).</summary>
public sealed class IfElseIfBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly IFunctionSupportedValue<TNode> _condition;

    internal IfElseIfBuilder(TLioScript<TNode> script, IFunctionSupportedValue<TNode> condition)
    {
        _script = script;
        _condition = condition;
    }

    public IfElseElseBuilder<TNode> If(TLioScript<TNode> ifScript)
        => new(_script, _condition, ifScript);
}

/// <summary>Call Else(elseScript) to complete the IfElse command.</summary>
public sealed class IfElseElseBuilder<TNode>
{
    private readonly TLioScript<TNode> _script;
    private readonly IFunctionSupportedValue<TNode> _condition;
    private readonly TLioScript<TNode> _ifScript;

    internal IfElseElseBuilder(TLioScript<TNode> script, IFunctionSupportedValue<TNode> condition, TLioScript<TNode> ifScript)
    {
        _script = script;
        _condition = condition;
        _ifScript = ifScript;
    }

    public TLioScript<TNode> Else(TLioScript<TNode> elseScript)
    {
        _script.Add(new IfElse<TNode>(_condition, _ifScript, elseScript));
        return _script;
    }
}
