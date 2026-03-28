using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.SubstringTests.</summary>
[TestFixture]
public class SubstringTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""str"": ""Hello World"", ""start"": 6, ""len"": 5 }");
    }

    [Test] public void Substring_TooFewArgs_ReturnsFailed()
    {
        var fn = new Substring<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.str") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
