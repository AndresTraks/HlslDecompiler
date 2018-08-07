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
                    return TryGetDot3ProductGroup(node, allowMatrixColumn);
                case 4:
                    return TryGetDot4ProductGroup(node, allowMatrixColumn);
                default:
                    throw new ArgumentOutOfRangeException(nameof(dimension));
            }
        }

        public DotProductContext TryGetDotProductGroup(HlslTreeNode node, bool allowMatrixColumn = false)
        {
            var dot4 = TryGetDot4ProductGroup(node, allowMatrixColumn);
            if (dot4 != null)
            {
                return dot4;
            }

            var dot3 = TryGetDot3ProductGroup(node, allowMatrixColumn);
            if (dot3 != null)
            {
                return dot3;
            }

            return TryGetDot2ProductGroup(node, allowMatrixColumn);
        }

        private DotProductContext TryGetDot4ProductGroup(HlslTreeNode node, bool allowMatrixColumn)
        {
            // 4 by 4 dot product has a pattern of:
            // #1  dot3(abc, xyz) + dw
            // #2  dw + dot3(abc, xyz)

            if (!(node is AddOperation addition))
            {
                return null;
            }

            MultiplyOperation dw;
            DotProductContext innerAddition = TryGetDot3ProductGroup(addition.Addend1, allowMatrixColumn);
            if (innerAddition == null)
            {
                innerAddition = TryGetDot3ProductGroup(addition.Addend2, allowMatrixColumn);
                if (innerAddition == null)
                {
                    return null;
                }

                dw = addition.Addend1 as MultiplyOperation;
                if (dw == null)
                {
                    return null;
                }
            }
            else
            {
                dw = addition.Addend2 as MultiplyOperation;
                if (dw == null)
                {
                    return null;
                }
            }

            HlslTreeNode a = innerAddition.Value1[0];
            HlslTreeNode b = innerAddition.Value1[1];
            HlslTreeNode c = innerAddition.Value1[2];
            HlslTreeNode d = dw.Factor1;
            HlslTreeNode x = innerAddition.Value2[0];
            HlslTreeNode y = innerAddition.Value2[1];
            HlslTreeNode z = innerAddition.Value2[2];
            HlslTreeNode w = dw.Factor2;
            if (CanGroupComponents(c, d, allowMatrixColumn))
            {
                if (CanGroupComponents(z, w, allowMatrixColumn))
                {
                    return new DotProductContext(new[] { a, b, c, d }, new[] { x, y, z, w });
                }
            }

            return null;
        }

        private DotProductContext TryGetDot3ProductGroup(HlslTreeNode node, bool allowMatrixColumn)
        {
            // 3 by 3 dot product has a pattern of:
            // #1  dot(ab, xy) + c*z
            // #2  c*z + dot(ab, xy)

            if (!(node is AddOperation addition))
            {
                return null;
            }

            MultiplyOperation cz;
            DotProductContext innerAddition = TryGetDot2ProductGroup(addition.Addend1, allowMatrixColumn);
            if (innerAddition == null)
            {
                innerAddition = TryGetDot2ProductGroup(addition.Addend2, allowMatrixColumn);
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
            if (CanGroupComponents(b, c, allowMatrixColumn))
            {
                if (CanGroupComponents(y, z, allowMatrixColumn))
                {
                    return new DotProductContext(new[] { a, b, c }, new[] { x, y, z });
                }
            }

            return null;
        }

        private DotProductContext TryGetDot2ProductGroup(HlslTreeNode node, bool allowMatrixColumn)
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

            if (CanGroupComponents(a, b, allowMatrixColumn) == false)
            {
                if (CanGroupComponents(a, y, allowMatrixColumn) == false)
                {
                    return null;
                }
                Swap(ref b, ref y);
            }

            if (CanGroupComponents(x, y, allowMatrixColumn) == false)
            {
                return null;
            }

            return new DotProductContext(new[] { a, b }, new[] { x, y });
        }

        private bool CanGroupComponents(HlslTreeNode a, HlslTreeNode b, bool allowMatrixColumn)
        {
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
