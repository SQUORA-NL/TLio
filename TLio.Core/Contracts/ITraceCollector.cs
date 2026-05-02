using TLio.Core.Models;

namespace TLio.Core.Contracts;

public interface ITraceCollector
{
    void Record(TraceEntry entry);
}
