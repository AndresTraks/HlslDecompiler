using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

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
    
    /// <summary>
    /// Assignments whose new value this one reads, rather than the register's old
    /// value. Recorded before lowering, the only time the two are distinguishable.
    /// </summary>
    public List<TempAssignmentNode> DependsOnNewValueOf { get; } = [];

    public override string ToString()
    {
        return $"{TempVariable} = {Value}";
    }
}
