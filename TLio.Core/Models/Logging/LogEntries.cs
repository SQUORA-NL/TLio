namespace TLio.Core.Models.Logging;

public class LogEntries : List<LogEntry>
{
    public bool HasErrors => this.Any(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error ||
                                           e.Level == Microsoft.Extensions.Logging.LogLevel.Critical);
}
