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
    /// The variables whose value this assignment wants as this statement computes
    /// it, rather than as the register held it before. Recorded before lowering,
    /// which is the only moment the two are distinguishable: a value that feeds
    /// this one is a node inside it then, and a variable name afterwards.
    /// </summary>
    public List<TempAssignmentNode> DependsOnNewValueOf { get; } = [];

    public override string ToString()
    {
        return $"{TempVariable} = {Value}";
    }
}
