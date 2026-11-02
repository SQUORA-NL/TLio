using System.Reflection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Extensions.ETL;
using TLio.Extensions.Looping;
using TLio.Extensions.Math;
using TLio.Extensions.Text;
using TLio.Extensions.TimeDate;
using TLio.FormatConverter;
using TLio.FormatConverter.Json;
using TLio.FormatConverter.Xml;
using TLio.FormatConverter.Yaml;
using Converter = global::TLio.FormatConverter.Core.FormatConverter;

namespace TLio.Parity.Tests;

/// <summary>
/// The other half of <see cref="PackRegistrationTests"/>: that test proves <c>With*</c> and
/// <c>Register*</c> agree with each other; this one proves they agree with <i>the pack</i>.
/// Every concrete command and function class in a pack's assembly must be reachable from the
/// pack's single registration call. Add a class and forget to register it — or delete one that is
/// still registered — and this fails, naming the class, instead of the command silently parsing
/// as "unknown" in some host.
/// </summary>
[TestFixture]
public class PackCompletenessTests
{
    private static Converter NewConverter()
    {
        var converter = new Converter();
        converter.Register(new JsonFormatAdapter());
        converter.Register(new XmlFormatAdapter());
        converter.Register(new YamlFormatAdapter());
        return converter;
    }

    private static IEnumerable<TestCaseData> Packs()
    {
        yield return Case("Math", typeof(RegisterMathPack), o => o.WithMath());
        yield return Case("Text", typeof(TextRegistrar), o => o.WithText());
        yield return Case("TimeDate", typeof(RegisterTimeDatePack), o => o.WithTimeDate());
        yield return Case("ETL", typeof(RegisterETLPack), o => o.WithEtl());
        yield return Case("Looping", typeof(RegisterLoopingPack), o => o.WithLooping());
        yield return Case("FormatConversion", typeof(RegisterFormatConversionPack),
            o => o.WithFormatConversion(NewConverter(), "json"));
    }

    private static TestCaseData Case(string name, Type anchor, Func<ParseOptions<JToken>, ParseOptions<JToken>> with) =>
        new TestCaseData(anchor.Assembly, with).SetName($"{name}_RegistersEveryCommandAndFunctionItDefines");

    [TestCaseSource(nameof(Packs))]
    public void EveryCommandAndFunctionInThePackIsRegistered(
        Assembly pack, Func<ParseOptions<JToken>, ParseOptions<JToken>> with)
    {
        var bare = ParseOptions<JToken>.CreateDefault();
        var builtInCommands = Names(bare.CommandsProvider.GetRegisteredCommandNames(), n => bare.CommandsProvider.GetCommand(n));
        var builtInFunctions = Names(bare.FunctionsProvider.GetRegisteredFunctionNames(), n => bare.FunctionsProvider.GetFunction(n));

        var options = with(ParseOptions<JToken>.CreateDefault());
        var registered = Names(options.CommandsProvider.GetRegisteredCommandNames(), n => options.CommandsProvider.GetCommand(n))
            .Concat(Names(options.FunctionsProvider.GetRegisteredFunctionNames(), n => options.FunctionsProvider.GetFunction(n)))
            .ToHashSet();

        var defined = pack.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: true })
            .Where(t => Implements(t, typeof(ICommand<>)) || Implements(t, typeof(IFunction<>)))
            .ToList();

        Assert.That(defined, Is.Not.Empty, "the scan found no commands or functions — the test itself is broken");

        var missing = defined.Where(t => !registered.Contains(t)).Select(t => t.Name).OrderBy(n => n).ToList();
        Assert.That(missing, Is.Empty,
            $"defined in {pack.GetName().Name} but not registered by its With*/Register* call — add it to the pack's Register method");

        // …and nothing is registered under the pack's call that the scan did not find, so a
        // class that moved or was deleted cannot leave a dangling registration unnoticed.
        var before = builtInCommands.Concat(builtInFunctions).ToHashSet();
        var fromPack = registered.Where(t => !before.Contains(t) && t.Assembly == pack).ToList();
        Assert.That(fromPack.Except(defined), Is.Empty);
    }

    private static IEnumerable<Type> Names<T>(IEnumerable<string> names, Func<string, T?> get) where T : class =>
        names.Select(get).Where(x => x != null)
             .Select(x => x!.GetType())
             .Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t);

    private static bool Implements(Type t, Type openInterface) =>
        t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);
}
