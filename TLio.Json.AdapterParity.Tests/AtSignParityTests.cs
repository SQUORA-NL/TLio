using NUnit.Framework;

namespace TLio.Json.AdapterParity.Tests;

/// <summary>
/// TLio's own relative-path token: <c>@</c> / <c>@.field</c> (the current node inside forEach, decisionTable, resolve,
/// setProperties) and <c>@.&lt;--</c> (its parent). The adapter turns these into absolute paths and hands them to the
/// JSONPath engine, so the path text it builds must be something the engine reads back to the same node — including
/// when a key contains a space, a dot or a quote. Every script here runs through both adapters and must give the same document.
/// </summary>
[TestFixture]
public class AtSignParityTests
{
    public sealed record Case(string Name, string Input, string Script)
    {
        public override string ToString() => Name;
    }

    private static string Loop(string commands, string path = "$.items") =>
        $$"""[ { "command": "forEach", "path": "{{path}}", "commands": [ {{commands}} ] } ]""";

    public static IEnumerable<TestCaseData> Cases()
    {
        var cases = new List<Case>
        {
            new("at-dot-field", """{"items":[{"id":1},{"id":2}]}""", Loop("""{ "command": "add", "path": "@.tag", "value": "visited" }""")),
            new("bare-at-replace", """{"items":[{"v":"a"},{"v":"b"}]}""", Loop("""{ "command": "set", "path": "@", "value": { "o": "=fetch(@.v)", "t": "x" } }""")),
            new("at-read-writes-elsewhere", """{"items":[{"v":"a"},{"v":"b"},{"v":"c"}]}""", Loop("""{ "command": "put", "path": "$.last", "value": "=fetch(@.v)" }""")),
            new("at-deep-field", """{"items":[{"o":{"n":1}},{"o":{"n":2}}]}""", Loop("""{ "command": "add", "path": "@.o.copy", "value": "=fetch(@.o.n)" }""")),
            new("at-index-into-element", """{"items":[{"t":[5,6]},{"t":[7,8]}]}""", Loop("""{ "command": "add", "path": "@.first", "value": "=fetch(@.t[0])" }""")),
            new("nested-loops", """{"g":[{"m":[{"x":1},{"x":2}]},{"m":[{"x":3}]}]}""",
                Loop(Loop("""{ "command": "add", "path": "@.seen", "value": true }""", "$.g[0].m").Trim('[', ']', ' ', '\n'), "$.g")),
            new("null-element-in-loop", """{"items":[{"id":1},null,{"id":3}]}""", Loop("""{ "command": "add", "path": "@.tag", "value": "v" }""")),
            new("null-field-read", """{"items":[{"v":null},{"v":2}]}""", Loop("""{ "command": "put", "path": "$.last", "value": "=fetch(@.v)" }""")),

            // keys that are not plain identifiers: the absolute path built for @ must still address the right node
            new("key-with-space", """{"items":[{"first name":"a"},{"first name":"b"}]}""", Loop("""{ "command": "add", "path": "@.copy", "value": "=fetch(@['first name'])" }""")),
            new("element-under-spaced-parent", """{"my list":[{"id":1},{"id":2}]}""", Loop("""{ "command": "add", "path": "@.tag", "value": "x" }""", "$['my list']")),
            new("element-under-dotted-parent", """{"a.b":[{"id":1},{"id":2}]}""", Loop("""{ "command": "add", "path": "@.tag", "value": "x" }""", "$['a.b']")),
            new("element-under-dashed-parent", """{"a-b":[{"id":1},{"id":2}]}""", Loop("""{ "command": "add", "path": "@.tag", "value": "x" }""", "$['a-b']")),
            new("element-under-quoted-parent", """{"o'q":[{"id":1},{"id":2}]}""", Loop("""{ "command": "add", "path": "@.tag", "value": "x" }""", "$['o\\'q']")),

            // parent navigation, as script text
            new("scriptpath-of-parent", """{"p":{"name":"Alice"}}""",
                """[ { "command": "put", "path": "$.p.where", "value": "=scriptpath(@.<--)" } ]"""),
            new("scriptpath-grandparent", """{"r":{"c":{"leaf":1}}}""",
                """[ { "command": "put", "path": "$.r.c.leaf2", "value": "=scriptpath(@.<--.<--)" } ]"""),
            new("scriptpath-per-item", """{"items":[{"id":1},{"id":2}]}""",
                """[ { "command": "put", "path": "$.items[*].path", "value": "=scriptpath()" } ]"""),
            new("scriptpath-per-item-spaced-parent", """{"my list":[{"id":1},{"id":2}]}""",
                """[ { "command": "put", "path": "$['my list'][*].path", "value": "=scriptpath()" } ]"""),
            new("scriptpath-parent-in-array", """{"items":[{"id":1,"o":{"x":1}},{"id":2,"o":{"x":2}}]}""",
                """[ { "command": "put", "path": "$.items[*].o.up", "value": "=scriptpath(@.<--)" } ]"""),
            new("fetch-via-parent", """{"items":[{"id":1,"o":{"x":1}},{"id":2,"o":{"x":2}}]}""",
                """[ { "command": "put", "path": "$.items[*].o.parentId", "value": "=fetch(@.<--.id)" } ]"""),
            new("fetch-via-parent-spaced", """{"my list":[{"id":1,"o":{"x":1}},{"id":2,"o":{"x":2}}]}""",
                """[ { "command": "put", "path": "$['my list'][*].o.parentId", "value": "=fetch(@.<--.id)" } ]"""),
            new("fetch-sibling-on-null-slot", """{"items":[{"id":1,"gone":null},{"id":2,"gone":null}]}""",
                """[ { "command": "put", "path": "$.items[*].gone", "value": "=fetch(@.<--.id)" } ]"""),
            new("setproperties-at", """{"items":[{"a":1,"b":2},{"a":3,"b":4}]}""",
                """[ { "command": "setProperties", "path": "$.items[*]", "properties": ["a"], "value": "=toArray()" } ]"""),
            new("decisiontable-at", """{"rows":[{"flag":true,"a":1,"b":2},{"flag":false,"a":3,"b":4}]}""",
                """[ { "command": "put", "path": "$.rows[*].picked", "value": "=if(@.flag,@.a,@.b)" } ]"""),
        };

        foreach (var c in cases) yield return new TestCaseData(c).SetName(c.Name);
    }

    [TestCaseSource(nameof(Cases))]
    public void Both_adapters_resolve_at_and_parent_navigation_identically(Case c)
    {
        var newtonsoft = AdapterParityTests.RunNewtonsoftPublic(c.Input, c.Script);
        var systemText = AdapterParityTests.RunSystemTextPublic(c.Input, c.Script);

        Assert.That(systemText.Threw, Is.EqualTo(newtonsoft.Threw), $"Newtonsoft: {newtonsoft.Detail}\nSystemText: {systemText.Detail}");
        Assert.That(systemText.Success, Is.EqualTo(newtonsoft.Success), $"Newtonsoft log:\n{newtonsoft.Detail}\nSystemText log:\n{systemText.Detail}");
        TestContext.Out.WriteLine($"[{c.Name}] success={newtonsoft.Success} {systemText.Json}" + (newtonsoft.Success ? "" : "\n  LOG(newtonsoft): " + newtonsoft.Detail.Replace("\n", " | ")));
        if (newtonsoft.Json == null) return;
        Assert.That(AdapterParityTests.CanonicalPublic(systemText.Json!), Is.EqualTo(AdapterParityTests.CanonicalPublic(newtonsoft.Json)),
            $"Newtonsoft: {newtonsoft.Json}\nSystemText: {systemText.Json}");
    }
}
