namespace TLio.FormatConverter.Core.Exceptions;

/// <summary>
/// Thrown when a format operation is requested for a format ID that has not been registered.
/// </summary>
public sealed class FormatNotRegisteredException : Exception
{
    /// <summary>The format ID that was requested but not found.</summary>
    public string FormatId { get; }

    /// <summary>All format IDs currently registered at the time of the error.</summary>
    public IReadOnlyList<string> RegisteredIds { get; }

    /// <summary>Initialises a new instance with the unknown format ID and the list of registered IDs.</summary>
    public FormatNotRegisteredException(string formatId, IReadOnlyList<string> registeredIds)
        : base($"Format '{formatId}' is not registered. Registered formats: [{string.Join(", ", registeredIds)}].")
    {
        FormatId = formatId;
        RegisteredIds = registeredIds;
    }
}
