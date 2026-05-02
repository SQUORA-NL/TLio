using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using YamlDotNet.RepresentationModel;

namespace TLio.Mcp.Services;

public sealed class DocumentService
{
    public JToken ParseJson(string content)
    {
        try { return JToken.Parse(content); }
        catch (Exception ex) { throw new InvalidOperationException($"Invalid JSON: {ex.Message}", ex); }
    }

    public XElement ParseXml(string content)
    {
        try { return XElement.Parse(content); }
        catch (Exception ex) { throw new InvalidOperationException($"Invalid XML: {ex.Message}", ex); }
    }

    public YamlNode ParseYaml(string content)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(content));
            if (stream.Documents.Count == 0)
                return new YamlMappingNode();
            return stream.Documents[0].RootNode;
        }
        catch (Exception ex) { throw new InvalidOperationException($"Invalid YAML: {ex.Message}", ex); }
    }

    public string SerializeJson(JToken token) =>
        token.ToString(Newtonsoft.Json.Formatting.None);

    public string SerializeXml(XElement element) =>
        element.ToString(SaveOptions.DisableFormatting);

    public string SerializeYaml(YamlNode node)
    {
        var stream = new YamlStream();
        stream.Add(new YamlDocument(node));
        var writer = new StringWriter();
        stream.Save(writer, assignAnchors: false);
        var result = writer.ToString();
        // Strip the trailing "..." document-end marker added by YamlDotNet
        return result.TrimEnd().TrimEnd('.').TrimEnd();
    }
}
