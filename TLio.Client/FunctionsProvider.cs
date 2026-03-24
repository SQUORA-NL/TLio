using TLio.Core.Contracts;

namespace TLio.Client;

/// <summary>
/// Default registry of known function types.
/// </summary>
public class FunctionsProvider<TNode> : IFunctionsProvider<TNode>, IFunctionsProviderRegistrar<TNode>
{
    private readonly Dictionary<string, Func<IFunction<TNode>>> _registry = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string functionName, Func<IFunction<TNode>> factory) =>
        _registry[functionName] = factory;

    public IFunction<TNode>? GetFunction(string functionName) =>
        _registry.TryGetValue(functionName, out var factory) ? factory() : null;

    public IEnumerable<string> GetRegisteredFunctionNames() => _registry.Keys;
}
