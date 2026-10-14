namespace HlslDecompiler.Hlsl;

/// <summary>
/// One 32-bit word of a double, via `asuint`. That overload returns through out
/// parameters, so the writer hoists the call into a named variable, like
/// GetDimensions.
/// </summary>
public class DoubleBitsNode : HlslTreeNode, IHasComponentIndex
{
    public DoubleBitsNode(HlslTreeNode value, int componentIndex)
    {
        AddInput(value);
        ComponentIndex = componentIndex;
    }

    public HlslTreeNode Value => Inputs[0];

    /// <summary>0 for the low word, 1 for the high.</summary>
    public int ComponentIndex { get; }

    // Set when the call is hoisted.
    public TempVariableNode NamedAs { get; set; }

    public override string ToString()
    {
        return $"asuint({Value}).{"xy"[ComponentIndex]}";
    }
}
