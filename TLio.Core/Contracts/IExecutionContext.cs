using TLio.Core.Models.Logging;

namespace TLio.Core.Contracts;

/// <summary>
/// The runtime environment injected into every command and function execution.
/// All format-specific capabilities are accessed through the properties on this
/// interface — no command or function should ever reference a concrete type.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IExecutionContext<TNode>
{
    /// <summary>Path-expression based node selector (e.g. JsonPath, XPath).</summary>
    IItemsFetcher<TNode> ItemsFetcher { get; set; }

    /// <summary>Format-specific node creation and mutation operations.</summary>
    INodeAdapter<TNode> NodeAdapter { get; set; }

    /// <summary>Structured execution log.</summary>
    IExecutionLogger Logger { get; set; }

    ITraceCollector? TraceCollector { get; set; }

    /// <summary>
    /// The current loop item, set by <c>forEach</c> around each iteration of its nested script —
    /// <c>null</c> outside any loop. A path or value beginning with
    /// <see cref="IItemsFetcher{TNode}.CurrentItemPathIndicator"/> (<c>@</c>) resolves against
    /// this when it is set, letting a nested command address "the item this iteration is on"
    /// regardless of where the command's own <c>path</c> points — e.g. reading <c>@</c> while
    /// writing to an unrelated accumulator elsewhere in the document. Unset (<c>null</c>) by
    /// default, so every command outside a loop keeps its existing behaviour unchanged.
    /// </summary>
    TNode? CurrentNode { get; set; }

    void LogWarning(string group, string message);
    void LogError(string group, string message);
    void LogInfo(string group, string message);

    LogEntries GetLogEntries();
}
