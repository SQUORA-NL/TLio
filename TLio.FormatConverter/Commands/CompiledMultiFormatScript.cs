namespace TLio.FormatConverter;

/// <summary>
/// A multi-format script parsed once via <see cref="MultiFormatScriptRunner.Compile"/>, ready to
/// run against many input documents without re-parsing.
/// </summary>
/// <remarks>
/// Thread-safe: each <see cref="Run"/> call parses its own input document and asks the
/// underlying section executor for a fresh execution instance, the same guarantee
/// <see cref="TLio.Client.CompiledScript{TNode}"/> makes for a single-format script.
/// </remarks>
public sealed class CompiledMultiFormatScript
{
    private readonly MultiFormatScriptRunner _runner;
    private readonly string _initialFormatId;
    private readonly List<ScriptSection> _sections;
    private readonly object?[] _compiledSections;

    internal CompiledMultiFormatScript(
        MultiFormatScriptRunner runner,
        string initialFormatId,
        List<ScriptSection> sections,
        object?[] compiledSections)
    {
        _runner = runner;
        _initialFormatId = initialFormatId;
        _sections = sections;
        _compiledSections = compiledSections;
    }

    /// <summary>Run the compiled script against <paramref name="inputDocument"/>, in the format it was compiled for.</summary>
    public MultiFormatScriptResult Run(string inputDocument) =>
        _runner.RunCompiled(_initialFormatId, inputDocument, _sections, _compiledSections);
}
