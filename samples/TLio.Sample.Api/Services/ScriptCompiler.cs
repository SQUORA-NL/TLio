using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using TLio.Client;
using TLio.Core.Models;
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
            // One notation, compiled three times — once per document format the slug can later
            // be run against. Detecting it once rather than per engine keeps the three compiled
            // forms the same script; a script that reads as XML must not be read as JSON for the
            // JSON endpoint just because that engine was asked first.
            var notation = ScriptFormatDetector.Detect(scriptSource);

            var jsonEngine = EngineSetup.CreateEngine<JToken>();
            var compiledJson = jsonEngine.Compile(scriptSource, notation, JsonExecutionContext.CreateDefault());

            var xmlEngine = EngineSetup.CreateEngine<XElement>();
            var compiledXml = xmlEngine.Compile(scriptSource, notation, XmlExecutionContext.CreateWithNativeXPath());

            var yamlEngine = EngineSetup.CreateEngine<YamlNode>();
            var compiledYaml = yamlEngine.Compile(scriptSource, notation, YamlExecutionContext.CreateDefault());

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
