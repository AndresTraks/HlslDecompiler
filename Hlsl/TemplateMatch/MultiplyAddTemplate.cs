namespace HlslDecompiler.Hlsl.TemplateMatch;

public class MultiplyAddTemplate : NodeTemplate<MultiplyAddOperation>
{
    public override HlslTreeNode Reduce(MultiplyAddOperation node)
    {
        // The multiply reads what the mad read. The matcher carries the mad's typing
        // over to the add it becomes, and nothing else would to the product inside
        // it: a uint uniform a float mad read as its bits had float readers
        // everywhere but there, and was converted for it.
        var multiplication = new MultiplyOperation(node.Factor1, node.Factor2)
        {
            ConsumesInteger = node.ConsumesInteger,
        };
        return new AddOperation(multiplication, node.Addend);
    }
}
