using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Core.Models;
using TLio.Extensions.Text;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.TextTests;

/// <summary>Ported from JLio.UnitTests.Text.NewGuidTests.</summary>
[TestFixture]
public class NewGuidTests
{
    private JToken data = null!;
    private TLio.Core.Contracts.IExecutionContext<JToken> context = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        data = JToken.Parse(@"{}");
    }

    [Test] public void NewGuid_ReturnsString()
    {
        var fn = new NewGuid<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var result = fn.Execute(data, data, context);
        Assert.That(result.Success, Is.True);
        var str = result.Data.First!.Value<string>()!;
        Assert.That(Guid.TryParse(str, out _), Is.True);
    }

    [Test] public void NewGuid_EachCallIsUnique()
    {
        var fn = new NewGuid<JToken>();
        fn.SetArguments(new Arguments<JToken>());
        var r1 = fn.Execute(data, data, context).Data.First!.Value<string>();
        var r2 = fn.Execute(data, data, context).Data.First!.Value<string>();
        Assert.That(r1, Is.Not.EqualTo(r2));
    }
}
