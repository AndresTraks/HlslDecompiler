using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// An atomic read-modify-write that keeps nothing: `atomic_iadd u0, r0.x, l(1)`,
/// which HLSL writes as `buffer.InterlockedAdd(offset, value)` on a byte address
/// buffer and as `InterlockedAdd(buffer[i], value)` on everything else.
///
/// A statement rather than a node, the way a store is: it has an effect and no
/// value, and the order it runs in relative to the loads around it is the whole
/// point of it.
/// </summary>
public class AtomicStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    public HlslTreeNode Address { get; set; }
    // The element byte offset, which a structured resource carries in the second
    // component of the address operand and a byte address one does not have.
    public HlslTreeNode ElementByteOffset { get; init; }
    // InterlockedCompareStore alone takes a value to compare against; everything
    // else leaves this null.
    public HlslTreeNode Compare { get; set; }
    public HlslTreeNode Value { get; set; }
    /// <summary>
    /// The variable the value the resource held before this goes into, for the
    /// imm_ forms, and null for the ones that keep nothing. HLSL spells it as the
    /// out parameter after the value - and InterlockedExchange and
    /// InterlockedCompareExchange have no form without it.
    /// </summary>
    public TempVariableNode Original { get; init; }
    public string MethodName { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public AtomicStatement(
        HlslTreeNode destination,
        HlslTreeNode address,
        HlslTreeNode value,
        string methodName,
        IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Destination = destination;
        Address = address;
        Value = value;
        MethodName = methodName;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }
}
