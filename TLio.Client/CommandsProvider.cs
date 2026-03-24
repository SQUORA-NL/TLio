using TLio.Core.Contracts;

namespace TLio.Client;

/// <summary>
/// Default registry of known command types.
/// External assemblies register commands via ICommandsProviderRegistrar.
/// </summary>
public class CommandsProvider<TNode> : ICommandsProvider<TNode>, ICommandsProviderRegistrar<TNode>
{
    private readonly Dictionary<string, Func<ICommand<TNode>>> _registry = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string commandName, Func<ICommand<TNode>> factory) =>
        _registry[commandName] = factory;

    public ICommand<TNode>? GetCommand(string commandName) =>
        _registry.TryGetValue(commandName, out var factory) ? factory() : null;

    public IEnumerable<string> GetRegisteredCommandNames() => _registry.Keys;
}
