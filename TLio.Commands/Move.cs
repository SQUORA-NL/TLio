using TLio.Commands.Logic;

namespace TLio.Commands;

/// <summary>
/// Moves (copy + remove) nodes from FromPath to ToPath.
/// Mirrors JLio's Move command exactly.
/// </summary>
public class Move<TNode> : CopyMoveBase<TNode>
{
    public Move() { }
    public Move(string from, string to) { FromPath = from; ToPath = to; }
    public Move(string from, string to, bool destinationAsArray) { FromPath = from; ToPath = to; DestinationAsArray = destinationAsArray; }

    protected override bool IsMove => true;
}
