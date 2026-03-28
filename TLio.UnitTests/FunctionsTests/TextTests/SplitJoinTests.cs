using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.UnitTests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.SplitTests and JoinTests.</summary>
[TestFixture]
public class SplitJoinTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{ ""csv"": ""a,b,c"", ""delim"": "","", ""arr"": [""x"", ""y"", ""z""], ""sep"": ""-"" }");
    }

    [Test] public void Split_TooFewArgs_ReturnsFailed()
    {
        var fn = new Split<JToken>();
        fn.SetArguments(new Arguments<JToken> { new PathValue<JToken>("$.csv") });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }

    [Test] public void Join_ArrayNotFound_ReturnsFailed()
    {
        var fn = new Join<JToken>();
        fn.SetArguments(new Arguments<JToken>
        {
            new PathValue<JToken>("$.missing"),
            new PathValue<JToken>("$.sep")
        });
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.False);
    }
}
