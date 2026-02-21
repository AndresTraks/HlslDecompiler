using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.TemplateMatch;

public class LengthTemplate : IGroupTemplate
{
    public IGroupContext Match(HlslTreeNode node)
    {
        if (node is SquareRootOperation sqrt && sqrt.Value is DotProductOperation dot)
        {
            if (NodeGrouper.AreNodesEquivalent(dot.X, dot.Y))
            {
                return new LengthContext(new GroupNode(new List<HlslTreeNode>(dot.X.Inputs).ToArray()));
            }
        }
        return null;
    }

    public HlslTreeNode Reduce(HlslTreeNode node, IGroupContext groupContext)
    {
        var lengthContext = groupContext as LengthContext;
        return new LengthOperation(lengthContext.Value);
    }
}
