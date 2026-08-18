namespace TLio.Commands.Advanced.Models;

/// <summary>Ordered set of <see cref="CompareResult"/> entries produced by one Compare run.</summary>
public class CompareResults : List<CompareResult>
{
    public CompareResults() { }

    public CompareResults(CompareResult result) => Add(result);

    public CompareResults(IEnumerable<CompareResult> items) : base(items) { }

    /// <summary>True when at least one entry records an actual difference.</summary>
    public bool ContainsDifference => this.Any(r => r.FoundDifference);
}
