using Microsoft.Extensions.Logging;
using TLio.Core.Contracts;
using TLio.Core.Models.Logging;

namespace TLio.Core.Models;

public class ExecutionLogger : IExecutionLogger
{
    public LogEntries LogEntries { get; } = new();

    public void Log(LogLevel level, string group, string message)
    {
        LogEntries.Add(new LogEntry(level, group, message, DateTimeOffset.UtcNow));
    }
}
