namespace HlslDecompiler.Hlsl;

public class GroupNode : HlslTreeNode
{
    public GroupNode(params HlslTreeNode[] components)
    {
        foreach (HlslTreeNode component in components)
        {
            AddInput(component);
        }
    }

    public int Length => Inputs.Count;

    public HlslTreeNode this[int index]
    {
        get => Inputs[index];
        set => Inputs[index] = value;
    }

    public override string ToString()
    {
        return $"({string.Join(",", Inputs)})";
    }
}
