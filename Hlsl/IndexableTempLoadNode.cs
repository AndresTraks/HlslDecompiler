using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of an element of an indexable temp, x0[i].y, read at run time.
/// A local array is memory rather than a register: a value graph cannot see
/// through a store at one index followed by a load at another, so a load is a
/// leaf that names the element rather than the value last written there, and a
/// store is a statement.
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
    public HlslTreeNode Index => Inputs[0];
    public int ComponentIndex { get; }

    public override string ToString()
    {
        return $"x{Register}[{Index}].{"xyzw"[ComponentIndex]}";
    }
}
