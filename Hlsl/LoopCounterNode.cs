namespace HlslDecompiler.Hlsl;

// The aL register, which `loop aL, iN` counts up and `c0[aL]` indexes by. It has
// no value in the tree: it is the enclosing loop's variable, and that variable is
// named by the writer, so the name is supplied from outside when compiling.
public class LoopCounterNode : HlslTreeNode, IHasComponentIndex
{
    public int ComponentIndex => 0;

    public override string ToString()
    {
        return "aL";
    }
}
