using TLio.Core.Contracts;
using TLio.Core.Models;

namespace TLio.Mcp.Services;

public sealed class McpTraceCollector : ITraceCollector
{
    private readonly List<TraceEntry> _entries = new();

    public IReadOnlyList<TraceEntry> Entries => _entries;

    public void Record(TraceEntry entry) => _entries.Add(entry);
}
