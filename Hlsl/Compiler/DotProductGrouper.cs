using System;

namespace HlslDecompiler.Hlsl
{
    public class DotProductGrouper
    {
        private readonly NodeGrouper _nodeGrouper;

        public DotProductGrouper(NodeGrouper nodeGrouper)
        {
            _nodeGrouper = nodeGrouper;
        }

        public DotProductContext TryGetDotProductGroup(HlslTreeNode node, int dimension, bool allowMatrixColumn = false)
        {
            switch (dimension)
            {
                case 2:
                    return TryGetDot2ProductGroup(node, allowMatrixColumn);
                case 3:
                    return TryGetDot3ProductGroup(node);
                case 4:
                    return TryGetDot3ProductGroup(node);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dimension));
            }
        }

        public DotProductContext TryGetDotProductGroup(HlslTreeNode node, bool allowMatrixColumn = false)
        {
            var dot4 = TryGetDot4ProductGroup(node);
            if (dot4 != null)
            {
                return dot4;
            }

            var dot3 = TryGetDot3ProductGroup(node);
            if (dot3 != null)
            {
                return dot3;
            }

            return TryGetDot2ProductGroup(node, allowMatrixColumn);
        }

        public DotProductContext TryGetDot4ProductGroup(HlslTreeNode node)
        {
            // 4 by 4 dot product has a pattern of:
            // #1  dot3(abc, xyz) + dw
            // #2  dw + dot3(abc, xyz)

            if (!(node is AddOperation addition))
            {
                return null;
            }

            DotProductContext innerAddition = TryGetDot3ProductGroup(addition.Addend1);
            if (innerAddition != null)
            {
                if (!(addition.Addend2 is MultiplyOperation dw))
                {
                    return null;
                }

                HlslTreeNode a = innerAddition.Value1[0];
                HlslTreeNode b = innerAddition.Value1[1];
                HlslTreeNode c = innerAddition.Value1[2];
                HlslTreeNode d = dw.Factor1;
                HlslTreeNode x = innerAddition.Value2[0];
                HlslTreeNode y = innerAddition.Value2[1];
                HlslTreeNode z = innerAddition.Value2[2];
                HlslTreeNode w = dw.Factor2;
                if (_nodeGrouper.CanGroupComponents(c, d))
                {
                    if (_nodeGrouper.CanGroupComponents(z, w))
                    {
                        return new DotProductContext(new[] { a, b, c, d }, new[] { x, y, z, w });
                    }
                }
            }

            return null;
        }

        public DotProductContext TryGetDot3ProductGroup(HlslTreeNode node)
        {
            // 3 by 3 dot product has a pattern of:
            // #1  dot(ab, xy) + c*z
            // #2  c*z + dot(ab, xy)

            if (!(node is AddOperation addition))
            {
                return null;
            }

            MultiplyOperation cz;
            DotProductContext innerAddition = TryGetDot2ProductGroup(addition.Addend1);
            if (innerAddition == null)
            {
                innerAddition = TryGetDot2ProductGroup(addition.Addend2);
                if (innerAddition == null)
                {
                    return null;
                }

                cz = addition.Addend1 as MultiplyOperation;
                if (cz == null)
                {
                    return null;
                }
            }
            else
            {
                cz = addition.Addend2 as MultiplyOperation;
                if (cz == null)
                {
                    return null;
                }
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

        public DotProductContext TryGetDot2ProductGroup(HlslTreeNode node, bool allowMatrixColumn = false)
        {
            // 2 by 2 dot product has a pattern of:
            // a*x + b*y

            if (!(node is AddOperation addition) ||
                !(addition.Addend1 is MultiplyOperation ax) ||
                !(addition.Addend2 is MultiplyOperation by))
            {
                return null;
            }

            HlslTreeNode a = ax.Factor1;
            HlslTreeNode b = by.Factor1;
            HlslTreeNode x = ax.Factor2;
            HlslTreeNode y = by.Factor2;
            if (a is ConstantNode || b is ConstantNode || x is ConstantNode || y is ConstantNode)
            {
                return null;
            }

            if (CanGroupComponents(a, b) == false)
            {
                if (CanGroupComponents(a, y) == false)
                {
                    return null;
                }
                Swap(ref b, ref y);
            }

            if (CanGroupComponents(x, y) == false)
            {
                return null;
            }

            return new DotProductContext(new[] { a, b }, new[] { x, y });
        }

        private bool CanGroupComponents(HlslTreeNode a, HlslTreeNode b)
        {
            const bool allowMatrixColumn = true;
            return _nodeGrouper.CanGroupComponents(a, b, allowMatrixColumn);
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

        public int Dimension => Value1.Length;
    }
}
