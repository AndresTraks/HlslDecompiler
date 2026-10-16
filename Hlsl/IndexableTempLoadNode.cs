using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A read of x0[i].y. Indexable temps are memory, so a load is a leaf naming the
/// element, not the value last stored there.
/// </summary>
public class IndexableTempLoadNode : HlslTreeNode, IHasComponentIndex
{
    public IndexableTempLoadNode(int register, HlslTreeNode index, int componentIndex)
    {
        Register = register;
        AddInput(index);
        ComponentIndex = componentIndex;
    }

    public int Register { get; }
    // Declared int: read where a float is wanted, it is the float's bits.
    public bool IsIntegerElement { get; init; }
    public HlslTreeNode Index => Inputs[0];
    public int ComponentIndex { get; }

    public override string ToString()
    {
        return $"x{Register}[{Index}].{"xyzw"[ComponentIndex]}";
    }
}
