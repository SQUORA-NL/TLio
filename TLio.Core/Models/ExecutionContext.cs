using Microsoft.Extensions.Logging;
using TLio.Core.Contracts;
using TLio.Core.Models.Logging;

namespace TLio.Core.Models;

public class ExecutionContext<TNode> : IExecutionContext<TNode>
{
    public required IItemsFetcher<TNode> ItemsFetcher { get; set; }
    public required INodeAdapter<TNode> NodeAdapter { get; set; }
    public IExecutionLogger Logger { get; set; } = new ExecutionLogger();
    public ITraceCollector? TraceCollector { get; set; }
    public TNode? CurrentNode { get; set; }

    public void LogWarning(string group, string message) =>
        Logger.Log(LogLevel.Warning, group, message);

    public void LogError(string group, string message) =>
        Logger.Log(LogLevel.Error, group, message);

    public void LogInfo(string group, string message) =>
        Logger.Log(LogLevel.Information, group, message);

    public LogEntries GetLogEntries() => Logger.LogEntries;
}
