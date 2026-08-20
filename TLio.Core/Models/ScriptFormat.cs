namespace TLio.Core.Models;

/// <summary>
/// The notation a script is written in. Orthogonal to the format of the data it runs against:
/// an XML script can transform a JSON document and vice versa, because every notation describes
/// the same command set. Only the path language inside the script has to match the data format.
/// </summary>
public enum ScriptFormat
{
    Json,
    Xml,
    Yaml
}
