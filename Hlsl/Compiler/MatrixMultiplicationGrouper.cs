using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class MatrixMultiplicationGrouper
    {
        private readonly NodeGrouper _nodeGrouper;
        private readonly RegisterState _registers;

        public MatrixMultiplicationGrouper(NodeGrouper nodeGrouper, RegisterState registers)
        {
            _nodeGrouper = nodeGrouper;
            _registers = registers;
        }

        public bool CanGroup(HlslTreeNode node1, HlslTreeNode node2)
        {
            return TryGetMultiplicationGroup(node1, node2) != null;
        }

        public MatrixMultiplicationContext TryGetMultiplicationGroup(HlslTreeNode node1, HlslTreeNode node2)
        {
            // 2x2(abcd) * 2x1(xy) matrix multiplication has a pattern of:
            // node1 = a*x + b*y
            // node2 = c*x + d*y

            // 1x2(xy) * 2x2(abcd) matrix multiplication has a pattern of:
            // node1 = a*x + c*y
            // node2 = b*x + d*y

            if (!(node1 is AddOperation add1))
            {
                return null;
            }
            if (!(node2 is AddOperation add2))
            {
                return null;
            }

            if (!(add1.Addend1 is MultiplyOperation ax))
            {
                return null;
            }
            if (!(add1.Addend2 is MultiplyOperation by))
            {
                return null;
            }
            if (!(add2.Addend1 is MultiplyOperation cx))
            {
                return null;
            }
            if (!(add2.Addend2 is MultiplyOperation dy))
            {
                return null;
            }

            HlslTreeNode x = GetCommonFactor(ax, cx);
            if (x == null)
            {
                return null;
                /*
                x = GetCommonFactor(ax, dy);
                if (x == null)
                {
                    return null;
                }
                Swap(ref cx, ref dy);
                */
            }
            if (!(GetOther(ax, x) is RegisterInputNode a))
            {
                return null;
            }
            if (!(GetOther(cx, x) is RegisterInputNode c))
            {
                return null;
            }

            HlslTreeNode y = GetCommonFactor(by, dy);
            if (y == null)
            {
                return null;
            }
            if (!(GetOther(by, y) is RegisterInputNode b))
            {
                return null;
            }
            if (!(GetOther(dy, y) is RegisterInputNode d))
            {
                return null;
            }

            if (_nodeGrouper.CanGroupComponents(x, y) == false)
            {
                return null;
            }

            bool matrixByVector;
            HlslTreeNode[] vector;
            ConstantDeclaration matrix = TryGet2x2MatrixDeclaration(a, b, c, d);
            if (matrix != null)
            {
                vector = new[] { x, y };
                matrixByVector = true;
            }
            else
            {
                matrix = TryGet2x2MatrixDeclaration(a, c, b, d);
                if (matrix != null)
                {
                    vector = new[] { y, x };
                    matrixByVector = false;
                }
                else
                {
                    return null;
                }
            }

            return new MatrixMultiplicationContext(vector, matrix, matrixByVector);
        }

        private ConstantDeclaration TryGet2x2MatrixDeclaration(
            RegisterInputNode a,
            RegisterInputNode b,
            RegisterInputNode c,
            RegisterInputNode d)
        {
            const bool ColumnMajorOrder = true;
            if (ColumnMajorOrder)
            {
                Swap(ref b, ref c);
            }

            RegisterComponentKey aKey = a.RegisterComponentKey;
            if (aKey.Type != RegisterType.Const)
            {
                return null;
            }
            ConstantDeclaration baseComponent = _registers.ConstantDeclarations.First(
                constant => constant.RegisterIndex == aKey.RegisterKey.Number);
            if (baseComponent.Rows != 2 || baseComponent.Columns != 2)
            {
                return null;
            }

            RegisterComponentKey cKey = c.RegisterComponentKey;
            if (cKey.Type != RegisterType.Const || cKey.Number != aKey.Number + 1)
            {
                return null;
            }

            RegisterComponentKey bKey = b.RegisterComponentKey;
            if (bKey.Type != RegisterType.Const || bKey.Number != aKey.Number || bKey.ComponentIndex == aKey.ComponentIndex)
            {
                return null;
            }

            RegisterComponentKey dKey = d.RegisterComponentKey;
            if (dKey.Type != RegisterType.Const || dKey.Number != cKey.Number || dKey.ComponentIndex == cKey.ComponentIndex)
            {
                return null;
            }

            return baseComponent;
        }

        private HlslTreeNode GetCommonFactor(MultiplyOperation ax, MultiplyOperation cx)
        {
            if (_nodeGrouper.AreNodesEquivalent(ax.Factor1, cx.Factor1) ||
                _nodeGrouper.AreNodesEquivalent(ax.Factor1, cx.Factor2))
            {
                return ax.Factor1;
            }

            if (_nodeGrouper.AreNodesEquivalent(ax.Factor2, cx.Factor1) ||
                _nodeGrouper.AreNodesEquivalent(ax.Factor2, cx.Factor2))
            {
                return ax.Factor2;
            }

            return null;
        }

        private HlslTreeNode GetOther(MultiplyOperation ax, HlslTreeNode x)
        {
            return _nodeGrouper.AreNodesEquivalent(ax.Factor1, x)
                ? ax.Factor2
                : ax.Factor1;
        }

        private static void Swap(ref MultiplyOperation a, ref MultiplyOperation b)
        {
            MultiplyOperation temp = a;
            a = b;
            b = temp;
        }

        private static void Swap(ref RegisterInputNode a, ref RegisterInputNode b)
        {
            RegisterInputNode temp = a;
            a = b;
            b = temp;
        }
    }

    public class MatrixMultiplicationContext
    {
        public MatrixMultiplicationContext(
            HlslTreeNode[] vector,
            ConstantDeclaration matrix,
            bool matrixByVector)
        {
            Vector = vector;
            MatrixDeclaration = matrix;
            IsMatrixByVector = matrixByVector;
        }

        public HlslTreeNode[] Vector { get; }

        public ConstantDeclaration MatrixDeclaration { get; }
        public bool IsMatrixByVector { get; }
    }
}
