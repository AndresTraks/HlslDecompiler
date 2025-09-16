namespace HlslDecompiler.Hlsl
{
    public class VirtualGroupNode : HlslTreeNode
    {
        public VirtualGroupNode(params HlslTreeNode[] components)
        {
            foreach (HlslTreeNode component in components)
            {
                // Skip registering component outputs
                Inputs.Add(component);
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
}
