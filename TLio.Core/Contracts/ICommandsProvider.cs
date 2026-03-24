namespace TLio.Core.Contracts;

/// <summary>
/// Registry of known command types. Used by the script parser to resolve
/// command names to ICommand implementations.
/// </summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface ICommandsProvider<TNode>
{
    ICommand<TNode>? GetCommand(string commandName);
    IEnumerable<string> GetRegisteredCommandNames();
}

/// <summary>Allows external assemblies to register their commands at startup.</summary>
public interface ICommandsProviderRegistrar<TNode>
{
    void Register(string commandName, Func<ICommand<TNode>> factory);
}
