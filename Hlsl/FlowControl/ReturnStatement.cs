using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class ReturnStatement : IStatement
{
    // retc_nz condition; null for an unconditional return.
    public HlslTreeNode Comparison { get; init; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public ReturnStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        : this(inputs, inputs)
    {
    }

    // Replacing an assignment statement, a return inherits its assignments.
    public ReturnStatement(
        IDictionary<RegisterComponentKey, HlslTreeNode> inputs,
        IDictionary<RegisterComponentKey, HlslTreeNode> outputs)
    {
        Inputs = inputs.ToDictionary();
        Outputs = outputs.ToDictionary();
    }

    /// <summary>A predicate is written out again rather than named: see <see
    /// cref="IStatement.NamedHeldNodes"/>.</summary>
    public IEnumerable<HeldSlot> HeldSlots => [HeldSlot.WrittenAgain(() => Comparison)];
}
