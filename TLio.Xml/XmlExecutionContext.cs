using System.Xml.Linq;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// Convenience factory for building an execution context wired up to the XML adapters.
/// </summary>
public static class XmlExecutionContext
{
    public static ExecutionContext<XElement> CreateDefault() => new()
    {
        ItemsFetcher = new XPathItemsFetcher(),
        NodeAdapter  = new XmlNodeAdapter(),
        Logger       = new ExecutionLogger()
    };
}
