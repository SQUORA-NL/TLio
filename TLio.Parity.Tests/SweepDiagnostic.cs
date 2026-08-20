using NUnit.Framework;

namespace TLio.Parity.Tests;

/// <summary>Diagnostic harness used while building the sweep — prints, never asserts shape.</summary>
[TestFixture, Explicit]
public class SweepDiagnostic
{
    [Test]
    public void Report()
    {
        var script = File.ReadAllText(Path.Combine(
            TestContext.CurrentContext.TestDirectory, "Sweep", "sweep.json"));

        var runs = new[]
        {
            SweepRunner.Run("JSON", script),
            SweepRunner.Run("XML",  script),
            SweepRunner.Run("YAML", script),
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
