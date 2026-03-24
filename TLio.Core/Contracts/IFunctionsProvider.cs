namespace TLio.Core.Contracts;

/// <summary>Registry of known function types.</summary>
/// <typeparam name="TNode">Native node type of the target data format.</typeparam>
public interface IFunctionsProvider<TNode>
{
    IFunction<TNode>? GetFunction(string functionName);
    IEnumerable<string> GetRegisteredFunctionNames();
}

/// <summary>Allows external assemblies to register their functions at startup.</summary>
public interface IFunctionsProviderRegistrar<TNode>
{
    void Register(string functionName, Func<IFunction<TNode>> factory);
}
