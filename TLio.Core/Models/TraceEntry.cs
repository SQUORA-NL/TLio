namespace TLio.Core.Models;

public record TraceEntry(
    string CommandName,
    string Path,
    TraceOutcome Outcome,
    int MatchedCount,
    string Detail
);
