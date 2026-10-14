namespace HlslDecompiler.Hlsl;

// One component of msad4; like lit, the destination component picks the window.
// The source is the intrinsic's two words, which the parser recovers from the
// window-building code fxc emits before the instruction.
public class Msad4Node : HlslTreeNode, IHasComponentIndex
{
    public Msad4Node(HlslTreeNode reference, HlslTreeNode sourceLow, HlslTreeNode sourceHigh,
        HlslTreeNode accumulator, int componentIndex)
    {
        AddInput(reference);
        AddInput(sourceLow);
        AddInput(sourceHigh);
        AddInput(accumulator);

        ComponentIndex = componentIndex;
    }

    public HlslTreeNode Reference => Inputs[0];
    public HlslTreeNode SourceLow => Inputs[1];
    public HlslTreeNode SourceHigh => Inputs[2];
    public HlslTreeNode Accumulator => Inputs[3];

    public int ComponentIndex { get; }
}
