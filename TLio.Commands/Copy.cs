using TLio.Commands.Logic;

namespace TLio.Commands;

/// <summary>
/// Copies the nodes matched by FromPath to ToPath.
/// Mirrors JLio's Copy command exactly.
/// </summary>
public class Copy<TNode> : CopyMoveBase<TNode>
{
    public Copy() { }
    public Copy(string from, string to) { FromPath = from; ToPath = to; }
    public Copy(string from, string to, bool destinationAsArray) { FromPath = from; ToPath = to; DestinationAsArray = destinationAsArray; }

    protected override bool IsMove => false;
}
