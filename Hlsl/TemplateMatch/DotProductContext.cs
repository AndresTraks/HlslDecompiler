using System;

namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class DotProductContext : IGroupContext
    {
        public DotProductContext(GroupNode value1, GroupNode value2)
        {
            if (value1.Length != value2.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(value1));
            }

            Value1 = value1;
            Value2 = value2;

            OrderByComponent();
        }

        public GroupNode Value1 { get; private set; }
        public GroupNode Value2 { get; private set; }

        public int Dimension => Value1.Length;

        private void OrderByComponent()
        {
            int[] components = GetComponentsIfUniqueAndUnordered();
            if (components == null)
            {
                return;
            }

            int dimension = components.Length;
            var newValue1 = new HlslTreeNode[dimension];
            var newValue2 = new HlslTreeNode[dimension];
            for (int i = 0; i < dimension; i++)
            {
                int min = int.MaxValue;
                int minIndex = 0;
                for (int j = 0; j < dimension; j++)
                {
                    if (components[j] < min)
                    {
                        min = components[j];
                        minIndex = j;
                    }
                }
                newValue1[i] = Value1[minIndex];
                newValue2[i] = Value2[minIndex];
                components[minIndex] = int.MaxValue;
            }
            Value1 = new GroupNode(newValue1);
            Value2 = new GroupNode(newValue2);
        }

        private int[] GetComponentsIfUniqueAndUnordered()
        {
            if (ValuesHaveSameComponents() == false)
            {
                return null;
            }

            bool isUnordered = false;
            var components = new int[Dimension];
            for (int i = 0; i < Dimension; i++)
            {
                // TODO: get child component index

                var componentIndex = (Value1[i] as IHasComponentIndex).ComponentIndex;
                if (Array.IndexOf(components, componentIndex, 0, i) != -1)
                {
                    return null;
                }

                components[i] = componentIndex;

                if (componentIndex != i)
                {
                    isUnordered = true;
                }
            }

            return isUnordered ? components : null;
        }

        private bool ValuesHaveSameComponents()
        {
            for (int i = 0; i < Dimension; i++)
            {
                // TODO: get child component index

                if (!(Value1[i] is IHasComponentIndex value1))
                {
                    return false;
                }

                if (!(Value2[i] is IHasComponentIndex value2))
                {
                    return false;
                }

                if (value1.ComponentIndex != value2.ComponentIndex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
