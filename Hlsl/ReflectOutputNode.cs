namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of a reflection. HLSL defines `reflect(i, n)` as
/// `i - 2 * dot(i, n) * n`, and that is what fxc writes - a dot, an add of the
/// result to itself, and a mad per component with the scale negated - so the idiom
/// is as many components as the vectors have, all sharing the two of them.
///
/// A node rather than something the compiler recognises out of the components it is
/// about to write, for the reason IdiomRecovery gives.
/// </summary>
public class ReflectOutputNode : HlslTreeNode, IHasComponentIndex
{
    public ReflectOutputNode(GroupNode incident, GroupNode normal, int componentIndex)
    {
        AddInput(incident);
        AddInput(normal);
        ComponentIndex = componentIndex;
    }

    public GroupNode Incident => (GroupNode)Inputs[0];

    public GroupNode Normal => (GroupNode)Inputs[1];

    public int ComponentIndex { get; }

    public override string ToString()
    {
        return $"reflect({Incident}, {Normal}).{"xyzw"[ComponentIndex]}";
    }
}
