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

    void LogWarning(string group, string message);
    void LogError(string group, string message);
    void LogInfo(string group, string message);

    LogEntries GetLogEntries();
}
