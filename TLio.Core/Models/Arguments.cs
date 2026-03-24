using TLio.Core.Contracts;

namespace TLio.Core.Models;

/// <summary>
/// The argument list passed to a function call in a script expression.
/// Each argument is itself an IFunctionSupportedValue so arguments can be nested.
/// </summary>
public class Arguments<TNode> : List<IFunctionSupportedValue<TNode>>
{
    public Arguments() { }
    public Arguments(IEnumerable<IFunctionSupportedValue<TNode>> args) : base(args) { }
}
