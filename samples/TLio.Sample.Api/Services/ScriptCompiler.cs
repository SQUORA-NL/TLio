using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using TLio.Client;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using TLio.Sample.Api;
using TLio.Sample.Api.Registry;
using YamlDotNet.RepresentationModel;

namespace TLio.Sample.Api.Services;

internal sealed class ScriptCompiler
{
    public ScriptRegistryEntry Compile(string slug, string scriptSource)
    {
        try
        {
            var jsonEngine = EngineSetup.CreateEngine<JToken>();
            var compiledJson = jsonEngine.Compile(scriptSource, JsonExecutionContext.CreateDefault());

            var xmlEngine = EngineSetup.CreateEngine<XElement>();
            var compiledXml = xmlEngine.Compile(scriptSource, XmlExecutionContext.CreateWithNativeXPath());

            var yamlEngine = EngineSetup.CreateEngine<YamlNode>();
            var compiledYaml = yamlEngine.Compile(scriptSource, YamlExecutionContext.CreateDefault());

            return new ScriptRegistryEntry(slug, scriptSource, compiledJson, compiledXml, compiledYaml, DateTimeOffset.UtcNow);
        }
        catch (ScriptCompilationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ScriptCompilationException(ex.Message);
        }
    }
}
