using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Client;

/// <summary>
/// Fluent extension methods on TLioScript&lt;TNode&gt;.
///
/// Each method creates an intermediate builder. Call the builder's terminal
/// method (OnPath / To / Result / If+Else) to append the command to the script
/// and return the script for further chaining.
///
/// Example:
/// <code>
///   var script = new TLioScript&lt;JToken&gt;()
///       .Add(JValue.CreateString("hello")).OnPath("$.greeting")
///       .Copy().From("$.src").To("$.dst")
///       .Compare().From("$.a").To("$.b").Result("$.verdict");
/// </code>
/// </summary>
public static class TLioScriptExtensions
{
    // ── Value commands ────────────────────────────────────────────────────────

    /// <summary>Start an Add command. Call .OnPath(path) to complete.</summary>
    public static ValueOnPathBuilder<TNode> Add<TNode>(this TLioScript<TNode> script, TNode value)
        => new(script, "add", new FixedValue<TNode>(value));

    /// <summary>Start a Set command. Call .OnPath(path) to complete.</summary>
    public static ValueOnPathBuilder<TNode> Set<TNode>(this TLioScript<TNode> script, TNode value)
        => new(script, "set", new FixedValue<TNode>(value));

    /// <summary>Start a Put command. Call .OnPath(path) to complete.</summary>
    public static ValueOnPathBuilder<TNode> Put<TNode>(this TLioScript<TNode> script, TNode value)
        => new(script, "put", new FixedValue<TNode>(value));

    // ── Remove ────────────────────────────────────────────────────────────────

    /// <summary>Start a Remove command. Call .OnPath(path) to complete.</summary>
    public static RemoveOnPathBuilder<TNode> Remove<TNode>(this TLioScript<TNode> script)
        => new(script);

    // ── Copy / Move ───────────────────────────────────────────────────────────

    /// <summary>Start a Copy command. Call .From(path).To(path).</summary>
    public static CopyMoveFromBuilder<TNode> Copy<TNode>(this TLioScript<TNode> script)
        => new(script, isMove: false);

    /// <summary>Start a Move command. Call .From(path).To(path).</summary>
    public static CopyMoveFromBuilder<TNode> Move<TNode>(this TLioScript<TNode> script)
        => new(script, isMove: true);

    // ── Compare ───────────────────────────────────────────────────────────────

    /// <summary>Start a Compare command. Call .From(path).To(path).Result(path).</summary>
    public static CompareFromBuilder<TNode> Compare<TNode>(this TLioScript<TNode> script)
        => new(script);

    // ── Merge ─────────────────────────────────────────────────────────────────

    /// <summary>Start a Merge command. Call .From(path).To(path).</summary>
    public static MergeFromBuilder<TNode> Merge<TNode>(this TLioScript<TNode> script)
        => new(script);

    // ── IfElse ────────────────────────────────────────────────────────────────

    /// <summary>Start an IfElse command. Call .If(ifScript).Else(elseScript).</summary>
    public static IfElseIfBuilder<TNode> IfElse<TNode>(
        this TLioScript<TNode> script,
        IFunctionSupportedValue<TNode> condition)
        => new(script, condition);
}
