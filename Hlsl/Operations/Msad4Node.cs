namespace HlslDecompiler.Hlsl;

// One component of an msad4 result. The four components are the sums of absolute
// differences of the reference's four bytes against four windows sliding along an
// eight byte source, so - like lit - the destination component decides which value
// this is rather than which part of the source is read. The source is the two words
// the intrinsic takes; fxc builds the windows out of them in front of the
// instruction, and the parser reads them back off that.
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
