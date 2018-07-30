namespace HlslDecompiler.Hlsl
{
    public class DotProductGrouper
    {
        private readonly NodeGrouper _nodeGrouper;

        public DotProductGrouper(NodeGrouper nodeGrouper)
        {
            _nodeGrouper = nodeGrouper;
        }

        public bool IsDotProduct(HlslTreeNode node)
        {
            return TryGetDotProductGroup(node) != null;
        }

        public DotProductContext TryGetDotProductGroup(HlslTreeNode node)
        {
            DotProductContext dot3 = TryGetDot3ProductGroup(node);
            if (dot3 != null)
            {
                return dot3;
            }

            return TryGetDot2ProductGroup(node);
        }

        public DotProductContext TryGetDot3ProductGroup(HlslTreeNode node)
        {
            // 3 by 3 dot product has a pattern of:
            // #1  (a*x + b*y) + c*z
            // #2  c*z + (a*x + b*y)

            if (!(node is AddOperation addition))
            {
                return null;
            }

            DotProductContext innerAddition = TryGetDot2ProductGroup(addition.Addend1);
            if (innerAddition == null)
            {
                return null;
            }

            if (!(addition.Addend2 is MultiplyOperation cz))
            {
                return null;
            }

            HlslTreeNode a = innerAddition.Value1[0];
            HlslTreeNode b = innerAddition.Value1[1];
            HlslTreeNode c = cz.Factor1;
            HlslTreeNode x = innerAddition.Value2[0];
            HlslTreeNode y = innerAddition.Value2[1];
            HlslTreeNode z = cz.Factor2;
            if (_nodeGrouper.CanGroupComponents(b, c))
            {
                if (_nodeGrouper.CanGroupComponents(y, z))
                {
                    return new DotProductContext(new[] { a, b, c }, new[] { x, y, z });
                }
            }

            return null;
        }

        public DotProductContext TryGetDot2ProductGroup(HlslTreeNode node)
        {
            // 2 by 2 dot product has a pattern of:
            // a*x + b*y

            if (!(node is AddOperation addition))
            {
                return null;
            }

            if (!(addition.Addend1 is MultiplyOperation ax))
            {
                return null;
            }
            if (!(addition.Addend2 is MultiplyOperation by))
            {
                return null;
            }

            HlslTreeNode a = ax.Factor1;
            if (a is ConstantNode)
            {
                return null;
            }
            HlslTreeNode b = by.Factor1;
            if (b is ConstantNode)
            {
                return null;
            }
            if (_nodeGrouper.CanGroupComponents(a, b) == false)
            {
                return null;
            }

            HlslTreeNode x = ax.Factor2;
            if (x is ConstantNode)
            {
                return null;
            }
            HlslTreeNode y = by.Factor2;
            if (y is ConstantNode)
            {
                return null;
            }
            if (_nodeGrouper.CanGroupComponents(x, y) == false)
            {
                return null;
            }

            return new DotProductContext(new[] { a, b }, new[] { x, y });
        }

        private static void Swap(ref HlslTreeNode a, ref HlslTreeNode b)
        {
            HlslTreeNode temp = a;
            a = b;
            b = temp;
        }
    }

    public class DotProductContext
    {
        public DotProductContext(
            HlslTreeNode[] value1,
            HlslTreeNode[] value2)
        {
            Value1 = value1;
            Value2 = value2;
        }

        public HlslTreeNode[] Value1 { get; }
        public HlslTreeNode[] Value2 { get; }
    }
}
