using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// An atomic read-modify-write: `buffer.InterlockedAdd(offset, value)` on a byte
/// address buffer, `InterlockedAdd(buffer[i], value)` on everything else. A
/// statement because its order relative to the loads around it matters.
/// </summary>
public class AtomicStatement : IStatement
{
    public HlslTreeNode Destination { get; }
    public HlslTreeNode Address { get; set; }
    /// <summary>
    /// The texel coordinate on a typed texture, one component per dimension, the
    /// first being <see cref="Address"/>. Null for buffers and groupshared memory.
    /// </summary>
    public HlslTreeNode[] Coordinates { get; set; }
    // Second component of the address operand on a structured resource.
    public HlslTreeNode ElementByteOffset { get; init; }
    // Set only for InterlockedCompareStore.
    public HlslTreeNode Compare { get; set; }
    public HlslTreeNode Value { get; set; }
    /// <summary>
    /// The out variable receiving the old value for the imm_ forms, else null.
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

    /// <summary>
    /// Not the element byte offset, which the writer reads as a number to name the
    /// member, nor <see cref="Original"/>, which is an output.
    /// </summary>
    public IEnumerable<HeldSlot> HeldSlots =>
    [
        HeldSlot.Named(() => Address, value => Address = value),
        HeldSlot.Named(() => Coordinates),
        HeldSlot.Named(() => Value, value => Value = value),
        HeldSlot.Named(() => Compare, value => Compare = value),
    ];
}
