using NUnit.Framework;

namespace TLio.Parity.Tests;

/// <summary>Diagnostic harness used while building the sweep — prints, never asserts shape.</summary>
[TestFixture, Explicit]
public class SweepDiagnostic
{
    [Test]
    public void Report()
    {
        var testDir = TestContext.CurrentContext.TestDirectory;
        string Script(string format) => File.ReadAllText(Path.Combine(
            testDir, "Sweep", format == "XML" ? "sweep.xml" : "sweep.json"));

        var runs = new[]
        {
            SweepRunner.Run("JSON", Script("JSON")),
            SweepRunner.Run("XML",  Script("XML")),
            SweepRunner.Run("YAML", Script("YAML")),
        };

        foreach (var run in runs)
        {
            Console.WriteLine($"\n═══ {run.Format}  success={run.Success} ═══");
            Console.WriteLine(run.Report());
        }

        Console.WriteLine("\n═══ documents ═══");
        foreach (var run in runs)
            Console.WriteLine($"{run.Format,-5}: {run.Document}");

        var json = runs[0].Document;
        foreach (var run in runs.Skip(1))
            Console.WriteLine($"\n{run.Format} matches JSON: {run.Document == json}");
    }
}
