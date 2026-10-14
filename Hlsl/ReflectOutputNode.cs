namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of `reflect(i, n)` = `i - 2 * dot(i, n) * n`. A node, not a
/// compile-time grouping, for the reason IdiomRecovery gives.
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
