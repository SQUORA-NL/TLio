namespace TLio.FormatConverter.Core.Exceptions;

/// <summary>
/// Thrown when an adapter fails to parse input (<c>ToIM</c>) or serialise an IM tree (<c>FromIM</c>).
/// </summary>
public sealed class FormatParseException : Exception
{
    /// <summary>The format ID of the adapter that raised the error.</summary>
    public string FormatId { get; }

    /// <summary><c>"ToIM"</c> or <c>"FromIM"</c> indicating which operation failed.</summary>
    public string Operation { get; }

    /// <summary>Initialises a new instance with context about the failed operation.</summary>
    public FormatParseException(string formatId, string operation, string message, Exception? innerException = null)
        : base($"[{formatId}/{operation}] {message}", innerException)
    {
        FormatId = formatId;
        Operation = operation;
    }
}
