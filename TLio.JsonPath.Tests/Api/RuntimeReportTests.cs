using System.Runtime.InteropServices;
using NUnit.Framework;

namespace TLio.JsonPath.Tests.Api;

/// <summary>
/// Makes the runtime under test visible in the test log, so "it passed on .NET 8 / 9 / 10 / (preview of) 11" is something
/// a reader can see rather than something CI is trusted to have done. Also pins the one runtime-dependent code path.
/// </summary>
[TestFixture]
public class RuntimeReportTests
{
    [Test]
    public void Report_the_runtime_this_suite_is_running_on()
    {
        TestContext.Out.WriteLine($"RUNTIME: {RuntimeInformation.FrameworkDescription} ({RuntimeInformation.RuntimeIdentifier}); library built for: {BuiltFor}");
        Assert.That(Environment.Version.Major, Is.GreaterThanOrEqualTo(8));
    }

    private static string BuiltFor =>
#if NET10_0_OR_GREATER
        "net10.0+";
#elif NET9_0_OR_GREATER
        "net9.0";
#else
        "net8.0";
#endif

    [Test]
    public void Objects_wider_than_a_few_members_select_correctly_whatever_the_runtime_has_for_reading_member_N()
    {
        // .NET 8 has no JsonObject.GetAt, so the engine reads members through a cursor there; this exercises it with
        // wildcards, descendants, filters over objects and repeated queries between mutations.
        foreach (var dialect in new[] { JsonPathDialect.Newtonsoft, JsonPathDialect.Rfc9535 })
        {
            var o = new System.Text.Json.Nodes.JsonObject();
            for (var i = 0; i < 2_000; i++) o["k" + i] = i;
            var engine = new JsonPathEngine(new JsonPathOptions { Dialect = dialect });
            var all = engine.Select(dialect == JsonPathDialect.Rfc9535 ? "$.*" : "$.*", o);
            Assert.That(all, Has.Count.EqualTo(2_000));
            Assert.That(all.Select(m => m.Name), Is.EqualTo(Enumerable.Range(0, 2_000).Select(i => "k" + i)));

            // mutate, select again: a stale cursor would hand back the old value
            o["k1500"] = -1;
            o.Remove("k3");
            var after = engine.Select("$..*", o);
            Assert.That(after, Has.Count.EqualTo(dialect == JsonPathDialect.Newtonsoft ? 2_000 : 1_999));
            Assert.That(after.Any(m => m.Node is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<int>(out var x) && x == -1), Is.True);
            Assert.That(after.Any(m => m.Name == "k3"), Is.False);
        }
    }
}
