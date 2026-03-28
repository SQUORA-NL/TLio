using System.Xml.Linq;
using TLio.Core.Models;

namespace TLio.Xml;

/// <summary>
/// Convenience factory for building an execution context wired up to the XML adapters.
/// </summary>
public static class XmlExecutionContext
{
    /// <summary>
    /// Slash-path convention: paths use a leading <c>/</c> stripped to relative XPath.
    /// Examples: <c>/name</c>, <c>/address/city</c>.
    /// </summary>
    public static ExecutionContext<XElement> CreateWithSlashPaths() => new()
    {
        ItemsFetcher = new SlashPathItemsFetcher(),
        NodeAdapter  = new XmlNodeAdapter(),
        Logger       = new ExecutionLogger()
    };

    /// <summary>
    /// Native XPath: paths are genuine XPath relative expressions — no stripping.
    /// Examples: <c>name</c>, <c>address/city</c>, <c>//name</c>, <c>items/item[1]</c>.
    /// </summary>
    public static ExecutionContext<XElement> CreateWithNativeXPath() => new()
    {
        ItemsFetcher = new NativeXPathItemsFetcher(),
        NodeAdapter  = new XmlNodeAdapter(),
        Logger       = new ExecutionLogger()
    };

    /// <summary>Backward-compat alias for <see cref="CreateWithSlashPaths"/>.</summary>
    public static ExecutionContext<XElement> CreateDefault() => CreateWithSlashPaths();
}
