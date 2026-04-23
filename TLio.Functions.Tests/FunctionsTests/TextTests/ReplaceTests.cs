using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.ReplaceTests.</summary>
[TestFixture]
public class ReplaceTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""Hello World"", ""old"": ""World"", ""new"": ""TLio"" }");
    }

    [Test] public void Replace_NoOccurrence()
    {
        var data2 = JToken.Parse(@"{ ""str"": ""Hello World"", ""old"": ""xyz"", ""new"": ""abc"" }");
        var fn = new Replace<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.old"),
            new PathValue<JToken>("$.new")
        });
        var result = fn.Execute(data2, data2, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello World"));
    }

    [Test] public void Replace_TooFewArgs_ReturnsFailed()
    {
        var fn = new Replace<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.old")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Replace_OccurrenceFound_ReplacesValue()
    {
        var fn = new Replace<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.old"),
            new PathValue<JToken>("$.new")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello TLio"));
    }

    [Test] public void Replace_WithEmptyReplacement_DeletesOccurrence()
    {
        var d = JToken.Parse(@"{ ""str"": ""Hello World"", ""old"": ""World"", ""new"": """" }");
        var fn = new Replace<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.old"),
            new PathValue<JToken>("$.new")
        });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("Hello "));
    }

    [Test] public void Replace_MultipleOccurrences_ReplacesAll()
    {
        var d = JToken.Parse(@"{ ""str"": ""aXbXc"", ""old"": ""X"", ""new"": ""-"" }");
        var fn = new Replace<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.str"),
            new PathValue<JToken>("$.old"),
            new PathValue<JToken>("$.new")
        });
        var result = fn.Execute(d, d, context);
        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.First!.Value<string>(), Is.EqualTo("a-b-c"));
    }
}
