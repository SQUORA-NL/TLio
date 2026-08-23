using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TLio.Client;
using TLio.Core.Contracts;
using TLio.Core.Models;
using TLio.Json;

namespace TLio.UnitTests.CommandsTests;

/// <summary>
/// The trace a write command records — Success, NoOp or Failure, and the detail text that goes
/// with it — is derived by reading back the log entries the command itself just wrote. That
/// read-back sits behind a null check on the collector, because it costs a list and three
/// substring scans per command and no ordinary host collects a trace.
///
/// These tests pin the traced side of that check: every outcome still has to be classified the
/// way it was, so the guard cannot be widened into skipping work that a trace needs. The
/// untraced side is pinned by the same assertions on the document and the log — which are what
/// a host without a collector actually observes, and which must not depend on the collector
/// being attached.
///
/// The sweep in TLio.Parity.Tests covers the Success outcome across all three formats; only
/// this file reaches NoOp and Failure.
/// </summary>
[TestFixture]
public class PropertyChangeCommandTraceTests
{
    private sealed class RecordingCollector : ITraceCollector
    {
        public List<TraceEntry> Entries { get; } = new();
        public void Record(TraceEntry entry) => Entries.Add(entry);
    }

    private static (List<TraceEntry> Trace, string Document, int LogCount) Run(string script, bool collectTrace)
    {
        var options = ParseOptions<JToken>.CreateDefault();
        var engine = new ScriptEngine<JToken>(options.CommandsProvider, options.FunctionsProvider);
        var context = JsonExecutionContext.CreateDefault();
        var collector = collectTrace ? new RecordingCollector() : null;
        context.TraceCollector = collector;

        var result = engine.Execute(script, JToken.Parse("{}"), context);

        return (collector?.Entries ?? new List<TraceEntry>(),
                result.Data.ToString(Newtonsoft.Json.Formatting.None),
                context.GetLogEntries().Count);
    }

    // ── The three outcomes ────────────────────────────────────────────────────

    [Test]
    public void AWriteThatLands_IsTracedAsSuccess()
    {
        var (trace, _, _) = Run("""[{ "command": "put", "path": "$.a.b", "value": 1 }]""", collectTrace: true);

        Assert.That(trace, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(trace[0].Outcome, Is.EqualTo(TraceOutcome.Success));
            Assert.That(trace[0].MatchedCount, Is.EqualTo(1));
            Assert.That(trace[0].Detail, Does.Contain("successfully applied"));
        });
    }

    [Test]
    public void ASetOnAFieldThatIsNotThere_IsTracedAsNoOp()
    {
        var (trace, _, _) = Run("""[{ "command": "set", "path": "$.nope", "value": 2 }]""", collectTrace: true);

        Assert.That(trace, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(trace[0].Outcome, Is.EqualTo(TraceOutcome.NoOp));
            Assert.That(trace[0].MatchedCount, Is.EqualTo(0));
            Assert.That(trace[0].Detail, Does.Contain("matched 0 nodes"));
        });
    }

    [Test]
    public void AnAddOntoAPropertyThatExists_IsTracedAsNoOp()
    {
        var (trace, _, _) = Run("""
            [ { "command": "put", "path": "$.a", "value": 1 },
              { "command": "add", "path": "$.a", "value": 2 } ]
            """, collectTrace: true);

        Assert.That(trace, Has.Count.EqualTo(2));
        Assert.That(trace[1].Outcome, Is.EqualTo(TraceOutcome.NoOp),
            "add onto an existing property skips, and the trace has to say so rather than claim a write");
    }

    [Test]
    public void AValueThatCannotBeComputed_IsTracedAsFailure()
    {
        var (trace, _, _) = Run(
            """[{ "command": "put", "path": "$.x", "value": "=sum($.nowhere)" }]""", collectTrace: true);

        Assert.That(trace, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(trace[0].Outcome, Is.EqualTo(TraceOutcome.Failure));
            Assert.That(trace[0].Detail, Does.Contain("failed at"));
            Assert.That(trace[0].Detail, Does.Not.EndWith("failed at '$.x'."),
                "a failure trace carries the reason read back from the log, not just the path");
        });
    }

    [Test]
    public void ACommandIsClassifiedFromItsOwnLogEntries_NotFromWhatRanBeforeIt()
    {
        var (trace, _, _) = Run("""
            [ { "command": "set", "path": "$.nope", "value": 1 },
              { "command": "put", "path": "$.a",    "value": 2 } ]
            """, collectTrace: true);

        Assert.That(trace, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(trace[0].Outcome, Is.EqualTo(TraceOutcome.NoOp));
            Assert.That(trace[1].Outcome, Is.EqualTo(TraceOutcome.Success),
                "the second command wrote its value; the first command's no-match warning is not its own");
        });
    }

    // ── Attaching a collector changes nothing else ────────────────────────────

    [TestCase("""[{ "command": "put", "path": "$.a.b", "value": 1 }]""")]
    [TestCase("""[{ "command": "set", "path": "$.nope", "value": 2 }]""")]
    [TestCase("""[{ "command": "set", "path": "$.miss[*].x", "value": 3 }]""")]
    [TestCase("""[{ "command": "add", "path": "$.deep.x.y.z", "value": 4 }]""")]
    [TestCase("""[{ "command": "put", "path": "$.x", "value": "=sum($.nowhere)" }]""")]
    public void TheDocumentAndTheLogAreTheSameWithAndWithoutACollector(string script)
    {
        var traced = Run(script, collectTrace: true);
        var untraced = Run(script, collectTrace: false);

        Assert.Multiple(() =>
        {
            Assert.That(untraced.Document, Is.EqualTo(traced.Document));
            Assert.That(untraced.LogCount, Is.EqualTo(traced.LogCount),
                "the log is written by the command, not by the trace — collecting a trace must not add to it");
        });
    }
}
