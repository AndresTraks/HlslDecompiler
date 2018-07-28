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
            // 2x2 matrix multiplication has a pattern of:
            // node1 = A*X + B*Y
            // node2 = C*X + D*Y
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
                x = GetCommonFactor(ax, dy);
                if (x == null)
                {
                    return null;
                }
                Swap(ref cx, ref dy);
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

            ConstantDeclaration matrixDelaration = TryGet2x2MatrixDeclaration(a, b, c, d);
            if (matrixDelaration == null)
            {
                return null;
            }

            return new MatrixMultiplicationContext(a, b, c, d, x, y, matrixDelaration);
        }

        private ConstantDeclaration TryGet2x2MatrixDeclaration(
            RegisterInputNode a,
            RegisterInputNode b,
            RegisterInputNode c,
            RegisterInputNode d)
        {
            RegisterComponentKey aKey = a.RegisterComponentKey;
            if (aKey.Type != RegisterType.Const || aKey.ComponentIndex != 0)
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
            if (cKey.Type != RegisterType.Const || cKey.Number != aKey.Number || cKey.ComponentIndex != 1)
            {
                return null;
            }

            RegisterComponentKey bKey = b.RegisterComponentKey;
            if (bKey.Type != RegisterType.Const || bKey.Number != aKey.Number + 1 || bKey.ComponentIndex != 0)
            {
                return null;
            }

            RegisterComponentKey dKey = d.RegisterComponentKey;
            if (dKey.Type != RegisterType.Const || bKey.Number != bKey.Number || dKey.ComponentIndex != 1)
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

        private static void Swap(ref MultiplyOperation cx, ref MultiplyOperation dy)
        {
            MultiplyOperation temp = cx;
            cx = dy;
            dy = temp;
        }
    }

    public class MatrixMultiplicationContext
    {
        public MatrixMultiplicationContext(
            RegisterInputNode a,
            RegisterInputNode b, 
            RegisterInputNode c, 
            RegisterInputNode d,
            HlslTreeNode x,
            HlslTreeNode y,
            ConstantDeclaration matrixDelaration)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            X = x;
            Y = y;
            MatrixDelaration = matrixDelaration;
        }

        public RegisterInputNode A { get; }
        public RegisterInputNode B { get; }
        public RegisterInputNode C { get; }
        public RegisterInputNode D { get; }

        public HlslTreeNode X { get; }
        public HlslTreeNode Y { get; }

        public ConstantDeclaration MatrixDelaration { get; }
    }
}
