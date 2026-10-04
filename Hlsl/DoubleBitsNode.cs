namespace HlslDecompiler.Hlsl;

/// <summary>
/// One of the two 32-bit words a double is made of, which is `asuint`. The
/// instructions say nothing about the split: a double lives in a register pair,
/// and the pair is read as two dwords by storing it into something that is not a
/// double - a `uint2` member, a byte address buffer - which costs no instruction
/// the way the join does not.
///
/// HLSL has no expression for it. `asuint` over a double is the three argument
/// overload, which hands the two words back through out parameters, so the writer
/// has to name the result before anything can read a word of it - the same shape
/// a GetDimensions takes, and for the same reason.
/// </summary>
public class DoubleBitsNode : HlslTreeNode, IHasComponentIndex
{
    public DoubleBitsNode(HlslTreeNode value, int componentIndex)
    {
        AddInput(value);
        ComponentIndex = componentIndex;
    }

    /// <summary>The double being taken apart.</summary>
    public HlslTreeNode Value => Inputs[0];

    /// <summary>Which word this is: nought the low one, one the high one.</summary>
    public int ComponentIndex { get; }

    // The variable the writer named the call into. Set when the call statement is
    // hoisted, and read wherever the node itself is still the root of an output.
    public TempVariableNode NamedAs { get; set; }

    public override string ToString()
    {
        return $"asuint({Value}).{"xy"[ComponentIndex]}";
    }
}
