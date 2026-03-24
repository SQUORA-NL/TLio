using Microsoft.Extensions.Logging;
using TLio.Core.Models.Logging;

namespace TLio.Core.Contracts;

public interface IExecutionLogger
{
    LogEntries LogEntries { get; }
    void Log(LogLevel level, string group, string message);
}
