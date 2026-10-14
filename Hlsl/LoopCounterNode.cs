namespace HlslDecompiler.Hlsl;

// The aL loop register; its name is the enclosing loop's variable, supplied by
// the writer when compiling.
public class LoopCounterNode : HlslTreeNode, IHasComponentIndex
{
    public int ComponentIndex => 0;

    public override string ToString()
    {
        return "aL";
    }
}
