using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Functions.Schema;
using TLio.Json;

namespace TLio.Functions.Tests.FunctionsTests.SchemaTests;

/// <summary>
/// The schema reader on its own: what it keeps from a schema, in what order, and the seam the
/// discriminator selector of phase 2 plugs into. The functions built on it are covered by the
/// fixtures under <c>Fixtures/Schema</c>.
/// </summary>
[TestFixture]
public class SchemaShapeTests
{
    private IExecutionContext<JToken> context = null!;
    private List<string> warnings = null!;

    [SetUp]
    public void Setup()
    {
        context = JsonExecutionContext.CreateDefault();
        warnings = new List<string>();
    }

    private SchemaView<JToken> View(string schema, IBranchSelector<JToken>? selector = null)
    {
        var document = new SchemaDocument<JToken>(JToken.Parse(schema), context.NodeAdapter, warnings.Add);
        return new SchemaView<JToken>(document, new[] { document.Root }, selector ?? UnionBranchSelector<JToken>.Instance);
    }

    private static string[] Names(FlatSchema<JToken> flat) => flat.Properties.Select(p => p.Key).ToArray();

    [Test]
    public void Properties_KeepDeclarationOrder()
    {
        var flat = View(@"{ ""properties"": { ""z"": {}, ""a"": {}, ""m"": {} } }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "z", "a", "m" }));
    }

    [Test]
    public void AllOf_ContributesInMemberOrder_AndAPropertyKeepsItsFirstPosition()
    {
        var flat = View(@"{
            ""properties"": { ""own"": {} },
            ""allOf"": [
                { ""properties"": { ""b"": {}, ""own"": {} } },
                { ""properties"": { ""c"": {} } }
            ] }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "own", "b", "c" }));
    }

    [Test]
    public void OneOf_IsAUnion_ByDefault()
    {
        var flat = View(@"{ ""oneOf"": [
            { ""properties"": { ""a"": {} } },
            { ""properties"": { ""b"": {} } } ] }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void FirstBranchSelector_CommitsToTheFirstBranch()
    {
        var flat = View(@"{ ""anyOf"": [
            { ""properties"": { ""a"": {} } },
            { ""properties"": { ""b"": {} } } ] }", FirstBranchSelector<JToken>.Instance).Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void ThenAndElse_AreBranches_AndIfIsIgnored()
    {
        var flat = View(@"{
            ""if"": { ""properties"": { ""never"": {} } },
            ""then"": { ""properties"": { ""t"": {} } },
            ""else"": { ""properties"": { ""e"": {} } } }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "t", "e" }));
    }

    /// <summary>
    /// The phase-2 seam. A discriminator selector needs the data node to pick a branch; phase 1
    /// must already deliver it, so adding the selector is a new class and not a new traversal.
    /// </summary>
    [Test]
    public void BranchSelector_ReceivesTheDataNode_AndTheBranchKindsConstValues()
    {
        var seen = new List<(BranchKind Kind, JToken? Data, int Count)>();
        var selector = new RecordingSelector(seen);
        var data = JToken.Parse(@"{ ""kind"": ""car"" }");

        var flat = View(@"{ ""oneOf"": [
            { ""properties"": { ""kind"": { ""const"": ""car"" }, ""wheels"": {} } },
            { ""properties"": { ""kind"": { ""const"": ""plane"" }, ""wings"": {} } } ] }", selector).Flatten(data);

        Assert.That(seen, Has.Count.EqualTo(1));
        Assert.That(seen[0].Kind, Is.EqualTo(BranchKind.OneOf));
        Assert.That(seen[0].Data, Is.SameAs(data));
        Assert.That(seen[0].Count, Is.EqualTo(2));

        // The const values a discriminator would compare are readable off the property schemas.
        var kind = flat.Properties.Single(p => p.Key == "kind").Value;
        var allowed = kind.Seeds.SelectMany(s => s.AllowedValues).Select(v => (string?)v).ToArray();
        Assert.That(allowed, Is.EqualTo(new[] { "car", "plane" }));
    }

    [Test]
    public void LocalRef_IsFollowed_AndSiblingKeywordsStillCount()
    {
        var flat = View(@"{
            ""$defs"": { ""Name"": { ""properties"": { ""first"": {} } } },
            ""$ref"": ""#/$defs/Name"",
            ""properties"": { ""own"": {} } }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "own", "first" }));
    }

    [Test]
    public void DraftSevenDefinitions_AreResolved()
    {
        var flat = View(@"{
            ""definitions"": { ""N"": { ""properties"": { ""x"": {} } } },
            ""$ref"": ""#/definitions/N"" }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public void RemoteRef_IsIgnoredWithAWarning()
    {
        var flat = View(@"{ ""$ref"": ""https://example.com/schema.json"", ""properties"": { ""a"": {} } }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "a" }));
        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("not a local reference"));
    }

    [Test]
    public void UnresolvableRef_IsIgnoredWithAWarning()
    {
        View(@"{ ""$ref"": ""#/$defs/Missing"" }").Flatten(null);

        Assert.That(warnings, Has.Count.EqualTo(1));
        Assert.That(warnings[0], Does.Contain("does not resolve"));
    }

    [Test]
    public void ARefCycle_ThatNeverDescendsIntoAProperty_StillTerminates()
    {
        // A -> B -> A with no properties in between: expansion must stop, not loop.
        var flat = View(@"{
            ""$defs"": { ""A"": { ""$ref"": ""#/$defs/B"" }, ""B"": { ""$ref"": ""#/$defs/A"", ""properties"": { ""p"": {} } } },
            ""$ref"": ""#/$defs/A"" }").Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "p" }));
    }

    [Test]
    public void TupleItems_AreReadFromBothDrafts()
    {
        var draft7 = View(@"{ ""items"": [ { ""properties"": { ""a"": {} } }, { ""properties"": { ""b"": {} } } ] }").Flatten(null);
        var draft2020 = View(@"{ ""prefixItems"": [ { ""properties"": { ""a"": {} } }, { ""properties"": { ""b"": {} } } ] }").Flatten(null);

        Assert.That(draft7.PrefixItems, Has.Count.EqualTo(2));
        Assert.That(draft2020.PrefixItems, Has.Count.EqualTo(2));
        Assert.That(draft7.Items, Is.Null);
    }

    [TestCase(@"{}", false)]
    [TestCase(@"{ ""type"": ""object"" }", false)]
    [TestCase(@"{ ""properties"": {} }", true)]
    [TestCase(@"{ ""items"": {} }", true)]
    public void OnlyShapeKeywords_MakeASchemaNonOpaque(string schema, bool hasStructure)
    {
        Assert.That(View(schema).Flatten(null).HasStructure, Is.EqualTo(hasStructure));
    }

    [Test]
    public void AdditionalProperties_AllowedWinsOverForbiddenAcrossBranches()
    {
        var flat = View(@"{ ""allOf"": [
            { ""additionalProperties"": false },
            { ""additionalProperties"": { ""properties"": { ""x"": {} } } } ] }").Flatten(null);

        Assert.That(flat.Additional, Is.EqualTo(AdditionalMode.Allowed));
        Assert.That(flat.AdditionalView, Is.Not.Null);
    }

    [Test]
    public void SchemaText_KeepsPropertyOrder_AndIntegerDefaults()
    {
        var node = SchemaDocument<JToken>.FromJsonText(
            @"{ ""properties"": { ""z"": { ""default"": 5 }, ""a"": { ""default"": 2.5 } } }", context.NodeAdapter);
        var document = new SchemaDocument<JToken>(node, context.NodeAdapter, warnings.Add);
        var flat = new SchemaView<JToken>(document, new[] { document.Root }, UnionBranchSelector<JToken>.Instance).Flatten(null);

        Assert.That(Names(flat), Is.EqualTo(new[] { "z", "a" }));

        var z = flat.Properties[0].Value.Flatten(null);
        Assert.That(z.Default!.Type, Is.EqualTo(JTokenType.Integer));
        Assert.That(flat.Properties[1].Value.Flatten(null).Default!.Type, Is.EqualTo(JTokenType.Float));
    }

    // ── Discriminator selection (phase 2) ─────────────────────────────────────

    private string[] Discriminated(string schema, string data)
    {
        var document = new SchemaDocument<JToken>(JToken.Parse(schema), context.NodeAdapter, warnings.Add);
        var selector = new DiscriminatorBranchSelector<JToken>(document, context.NodeAdapter);
        var view = new SchemaView<JToken>(document, new[] { document.Root }, selector);
        return Names(view.Flatten(JToken.Parse(data)));
    }

    private const string CarOrPlane = @"{ ""oneOf"": [
        { ""properties"": { ""kind"": { ""const"": ""car"" }, ""wheels"": {} } },
        { ""properties"": { ""kind"": { ""const"": ""plane"" }, ""wings"": {} } } ] }";

    [Test]
    public void Discriminator_PicksTheOnlyBranchThatAllowsTheValue() =>
        Assert.That(Discriminated(CarOrPlane, @"{ ""kind"": ""plane"" }"), Is.EqualTo(new[] { "kind", "wings" }));

    [TestCase(@"{ ""kind"": ""boat"" }", Description = "no branch allows it")]
    [TestCase(@"{ ""wings"": 2 }", Description = "the data lacks the discriminator")]
    [TestCase(@"{ ""kind"": { ""x"": 1 } }", Description = "the value is not a scalar")]
    public void Discriminator_FallsBackToTheUnion_WhenItCannotDecide(string data) =>
        Assert.That(Discriminated(CarOrPlane, data), Is.EqualTo(new[] { "kind", "wheels", "wings" }));

    [Test]
    public void Discriminator_FallsBackToTheUnion_WhenTheDataIsNotAnObject() =>
        Assert.That(Discriminated(CarOrPlane, @"""plane"""), Is.EqualTo(new[] { "kind", "wheels", "wings" }));

    [Test]
    public void Discriminator_FallsBackToTheUnion_WhenABranchPinsNothing()
    {
        // The second branch has no discriminator, so "car" proves nothing about it.
        var schema = @"{ ""oneOf"": [
            { ""properties"": { ""kind"": { ""const"": ""car"" }, ""wheels"": {} } },
            { ""properties"": { ""wings"": {} } } ] }";

        Assert.That(Discriminated(schema, @"{ ""kind"": ""car"" }"), Is.EqualTo(new[] { "kind", "wheels", "wings" }));
    }

    [Test]
    public void Discriminator_TriesTheNextCandidate_WhenTheFirstIsAmbiguous()
    {
        // `family` does not separate the branches, `version` does.
        var schema = @"{ ""oneOf"": [
            { ""properties"": { ""family"": { ""const"": ""x"" }, ""version"": { ""const"": 1 }, ""a"": {} } },
            { ""properties"": { ""family"": { ""const"": ""x"" }, ""version"": { ""const"": 2 }, ""b"": {} } } ] }";

        Assert.That(Discriminated(schema, @"{ ""family"": ""x"", ""version"": 2 }"),
            Is.EqualTo(new[] { "family", "version", "b" }));
    }

    [Test]
    public void Discriminator_ReadsBranchesThroughRefAndAllOf()
    {
        var schema = @"{
            ""$defs"": {
                ""Base"": { ""properties"": { ""id"": {} } },
                ""Car"":   { ""allOf"": [ { ""$ref"": ""#/$defs/Base"" }, { ""properties"": { ""kind"": { ""const"": ""car"" }, ""wheels"": {} } } ] },
                ""Plane"": { ""allOf"": [ { ""$ref"": ""#/$defs/Base"" }, { ""properties"": { ""kind"": { ""const"": ""plane"" }, ""wings"": {} } } ] }
            },
            ""oneOf"": [ { ""$ref"": ""#/$defs/Car"" }, { ""$ref"": ""#/$defs/Plane"" } ] }";

        Assert.That(Discriminated(schema, @"{ ""kind"": ""car"" }"), Is.EqualTo(new[] { "id", "kind", "wheels" }));
    }

    [Test]
    public void Discriminator_TreatsNumbersAndBooleansTheSameWhateverTheTextForm()
    {
        var schema = @"{ ""oneOf"": [
            { ""properties"": { ""n"": { ""const"": 1 }, ""one"": {} } },
            { ""properties"": { ""n"": { ""const"": 2 }, ""two"": {} } } ] }";
        var flags = @"{ ""anyOf"": [
            { ""properties"": { ""on"": { ""const"": true }, ""yes"": {} } },
            { ""properties"": { ""on"": { ""const"": false }, ""no"": {} } } ] }";

        // XML and YAML scalars arrive as text.
        Assert.That(Discriminated(schema, @"{ ""n"": ""2"" }"), Is.EqualTo(new[] { "n", "two" }));
        Assert.That(Discriminated(schema, @"{ ""n"": 1.0 }"), Is.EqualTo(new[] { "n", "one" }));
        Assert.That(Discriminated(flags, @"{ ""on"": ""false"" }"), Is.EqualTo(new[] { "on", "no" }));
    }

    [Test]
    public void Discriminator_LeavesThenAndElseAUnion()
    {
        var schema = @"{
            ""if"": { ""properties"": { ""kind"": { ""const"": ""car"" } } },
            ""then"": { ""properties"": { ""kind"": { ""const"": ""car"" }, ""wheels"": {} } },
            ""else"": { ""properties"": { ""kind"": { ""const"": ""plane"" }, ""wings"": {} } } }";

        Assert.That(Discriminated(schema, @"{ ""kind"": ""car"" }"), Is.EqualTo(new[] { "kind", "wheels", "wings" }));
    }

    [Test]
    public void TheFlag_MustBeTheThirdArgumentOfOrderBySchema()
    {
        var (success, data) = Run(
            @"[{ ""command"": ""put"", ""path"": ""$.r"", ""value"": ""=orderBySchema($.s,$.d,'bogus')"" }]",
            @"{ ""s"": { ""properties"": { ""a"": {} } }, ""d"": { ""a"": 1 } }");

        Assert.That(success && data["r"] is not null, Is.False);
        Assert.That(context.GetLogEntries().Select(e => e.Message), Has.Some.Contain("must be 'discriminate'"));
    }

    // ── The functions' error paths ────────────────────────────────────────────

    private (bool Success, JToken Data) Run(string script, string document)
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var result = engine.Execute(script, JToken.Parse(document), context);
        return (result.Success, result.Data);
    }

    [Test]
    public void SchemaText_ThatIsNotJson_FailsWithAnError()
    {
        var (success, data) = Run(
            @"[{ ""command"": ""put"", ""path"": ""$.r"", ""value"": ""=orderBySchema('{not json', $.d)"" }]",
            @"{ ""d"": { ""a"": 1 } }");

        Assert.That(success && data["r"] is not null, Is.False);
        Assert.That(context.GetLogEntries().Select(e => e.Message), Has.Some.Contain("not valid JSON"));
    }

    [Test]
    public void MissingDataArgument_FailsWithAnError()
    {
        var (success, data) = Run(
            @"[{ ""command"": ""put"", ""path"": ""$.r"", ""value"": ""=orderBySchema($.s)"" }]",
            @"{ ""s"": { ""properties"": { ""a"": {} } } }");

        Assert.That(success && data["r"] is not null, Is.False);
        Assert.That(context.GetLogEntries().Select(e => e.Message), Has.Some.Contain("requires a schema"));
    }

    [Test]
    public void TheSourceDocument_IsNeverModified()
    {
        var (success, data) = Run(
            @"[{ ""command"": ""put"", ""path"": ""$.r"", ""value"": ""=orderBySchema($.s,$.d)"" }]",
            @"{ ""s"": { ""properties"": { ""b"": {}, ""a"": {} } }, ""d"": { ""a"": 1, ""b"": 2 } }");

        Assert.That(success, Is.True);
        Assert.That(data["d"]!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo(@"{""a"":1,""b"":2}"));
        Assert.That(data["r"]!.ToString(Newtonsoft.Json.Formatting.None), Is.EqualTo(@"{""b"":2,""a"":1}"));
    }

    private sealed class RecordingSelector : IBranchSelector<JToken>
    {
        private readonly List<(BranchKind Kind, JToken? Data, int Count)> _seen;

        public RecordingSelector(List<(BranchKind Kind, JToken? Data, int Count)> seen) => _seen = seen;

        public IReadOnlyList<SchemaNode<JToken>> Select(
            BranchKind kind, IReadOnlyList<SchemaNode<JToken>> alternatives, JToken? data)
        {
            _seen.Add((kind, data, alternatives.Count));
            return alternatives;
        }
    }
}
