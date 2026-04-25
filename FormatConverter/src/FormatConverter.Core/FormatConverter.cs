using FormatConverter.Core.Exceptions;
using FormatConverter.Core.Model;

namespace FormatConverter.Core;

/// <summary>
/// Central registry and dispatcher for format adapters.
/// Adapters are registered by callers; a second registration for the same format ID replaces the first.
/// All format ID comparisons are case-insensitive.
/// </summary>
public sealed class FormatConverter
{
    private readonly Dictionary<string, IFormatAdapter> _adapters =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Register or replace the adapter for its <see cref="IFormatAdapter.FormatId"/>.
    /// </summary>
    public void Register(IFormatAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        _adapters[adapter.FormatId] = adapter;
    }

    /// <summary>All format IDs currently registered (unordered).</summary>
    public IReadOnlyList<string> RegisteredFormats => _adapters.Keys.ToList();

    /// <summary>Parse a format string into an IM tree using the registered adapter.</summary>
    /// <exception cref="FormatNotRegisteredException">When <paramref name="formatId"/> is not registered.</exception>
    public IntermediateNode ToIM(string formatId, string source, ConversionSettings settings)
    {
        var adapter = Resolve(formatId);
        return adapter.ToIM(source, settings);
    }

    /// <summary>Serialise an IM tree into a format string using the registered adapter.</summary>
    /// <exception cref="FormatNotRegisteredException">When <paramref name="formatId"/> is not registered.</exception>
    public string FromIM(string formatId, IntermediateNode root, ConversionSettings settings)
    {
        var adapter = Resolve(formatId);
        return adapter.FromIM(root, settings);
    }

    /// <summary>
    /// Convenience one-step conversion: parse <paramref name="document"/> from <paramref name="sourceFormatId"/>
    /// and serialise to <paramref name="targetFormatId"/> using the same <paramref name="settings"/>.
    /// </summary>
    /// <exception cref="FormatNotRegisteredException">When either format is not registered.</exception>
    public string Convert(string sourceFormatId, string document, string targetFormatId, ConversionSettings settings)
    {
        var im = ToIM(sourceFormatId, document, settings);
        return FromIM(targetFormatId, im, settings);
    }

    private IFormatAdapter Resolve(string formatId)
    {
        if (_adapters.TryGetValue(formatId, out var adapter))
            return adapter;
        throw new FormatNotRegisteredException(formatId, _adapters.Keys.ToList());
    }
}
