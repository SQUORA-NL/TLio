using Microsoft.Extensions.Logging;

namespace TLio.Core.Models.Logging;

public record LogEntry(LogLevel Level, string Group, string Message, DateTimeOffset Timestamp);
