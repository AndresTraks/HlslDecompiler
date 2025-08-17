namespace HlslDecompiler.Hlsl
{
    public class TempAssignmentNode : HlslTreeNode, IHasComponentIndex
    {
        public TempAssignmentNode(TempVariableNode tempVariable, HlslTreeNode value)
        {
            AddInput(value);
            TempVariable = tempVariable;
        }

        public TempVariableNode TempVariable { get; set; }

        public HlslTreeNode Value => Inputs[0];
        public int ComponentIndex => TempVariable.ComponentIndex;

        public bool IsReassignment { get; set; } = false;

        public override string ToString()
        {
            return $"{TempVariable} = {Value}";
        }
    }
}
