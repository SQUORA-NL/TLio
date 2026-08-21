using TLio.Core.Models.Logging;

namespace FormatConverter.TLio;

/// <summary>The outcome of a multi-format script run.</summary>
/// <param name="Document">The final document, in <paramref name="FormatId"/>.</param>
/// <param name="FormatId">
/// The format the run ended in — the target of the last <c>convert</c>, or the input format when
/// the script converted nothing.
/// </param>
/// <param name="Success">Whether every command in every section succeeded.</param>
/// <param name="Logs">Everything the sections logged, in the order they ran.</param>
public sealed record MultiFormatScriptResult(string Document, string FormatId, bool Success, LogEntries Logs);
