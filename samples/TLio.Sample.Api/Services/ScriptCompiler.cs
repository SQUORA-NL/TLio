using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using TLio.Client;
using TLio.Json;
using TLio.Xml;
using TLio.Yaml;
using TLio.Sample.Api.Registry;
using YamlDotNet.RepresentationModel;

namespace TLio.Sample.Api.Services;

internal sealed class ScriptCompiler
{
    public ScriptRegistryEntry Compile(string slug, string scriptSource)
    {
        try
        {
            var jsonOptions = ParseOptions<JToken>.CreateDefault();
            var jsonEngine = new ScriptEngine<JToken>(jsonOptions.CommandsProvider, jsonOptions.FunctionsProvider);
            var compiledJson = jsonEngine.Compile(scriptSource, JsonExecutionContext.CreateDefault());

            var xmlOptions = ParseOptions<XElement>.CreateDefault();
            var xmlEngine = new ScriptEngine<XElement>(xmlOptions.CommandsProvider, xmlOptions.FunctionsProvider);
            var compiledXml = xmlEngine.Compile(scriptSource, XmlExecutionContext.CreateWithNativeXPath());

            var yamlOptions = ParseOptions<YamlNode>.CreateDefault();
            var yamlEngine = new ScriptEngine<YamlNode>(yamlOptions.CommandsProvider, yamlOptions.FunctionsProvider);
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
