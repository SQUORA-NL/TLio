using NUnit.Framework;

namespace TLio.Parity.Tests;

/// <summary>
/// Writes the recorded sweep documents back to the source tree. Explicit — run it deliberately
/// after a sweep change, then read the diff before committing it.
///
///   dotnet test TLio.Parity.Tests --filter "Name~RecordSweep"
/// </summary>
[TestFixture, Explicit]
public class SweepRecorder
{
    [Test]
    public void RecordSweep()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        var script  = File.ReadAllText(Path.Combine(testDir, "Sweep", "sweep.json"));

        // bin/Debug/net10.0 → project root
        var sourceDir = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", "Sweep"));

        foreach (var format in new[] { "JSON", "XML", "YAML" })
        {
            var run = SweepRunner.Run(format, script);
            Assert.That(run.Success, Is.True, $"{format} sweep failed; nothing recorded.\n{run.Report()}");
            Assert.That(run.NotApplied, Is.Empty, $"{format} sweep had unapplied commands.\n{run.Report()}");

            var path = Path.Combine(sourceDir, $"expected.{format.ToLowerInvariant()}.json");
            File.WriteAllText(path, run.Document + "\n");
            Console.WriteLine($"RECORDED {path}");
        }
    }
}
