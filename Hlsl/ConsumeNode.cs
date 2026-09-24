namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of what `input.Consume()` returns. A consume buffer takes the
/// last slot from its counter and reads the element there, which the bytecode
/// writes as an imm_atomic_consume and then one ld_structured per component -
/// four of them for a float4, at byte offsets 0, 4, 8 and 12.
///
/// A ConsumeStructuredBuffer has no other spelling: no subscript, and no counter
/// to read the slot out of. So the call has to be named and its components read
/// out of the variable afterwards, the way a GetDimensions result is - and for
/// the same reason, that one call fills several values at once.
/// </summary>
public class ConsumeNode : HlslTreeNode, IHasComponentIndex
{
    public ConsumeNode(RegisterInputNode buffer, int componentIndex, HlslTreeNode slot)
    {
        AddInput(buffer);
        ComponentIndex = componentIndex;
        Slot = slot;
    }

    public RegisterInputNode Buffer => (RegisterInputNode)Inputs[0];
    public int ComponentIndex { get; }

    /// <summary>
    /// The value the consume instruction put in a register, which says which call
    /// this is a component of. Held rather than read: nothing in the graph reads
    /// the slot once the loads have become these, which is what lets the
    /// assignment the consume made go away as dead.
    /// </summary>
    public HlslTreeNode Slot { get; }

    /// <summary>The variable the writer named this call into.</summary>
    public TempVariableNode NamedAs { get; set; }

    public override string ToString()
    {
        return $"consume({Buffer}).{"xyzw"[ComponentIndex]}";
    }
}

/// <summary>
/// What an imm_atomic_consume leaves in a register: the slot it took. Nothing
/// reads it once the loads from the buffer have become <see cref="ConsumeNode"/>s,
/// so the assignment goes away and the call stands for both instructions.
/// </summary>
public class ConsumeSlotNode : HlslTreeNode
{
    public ConsumeSlotNode(RegisterInputNode buffer)
    {
        AddInput(buffer);
    }

    public RegisterInputNode Buffer => (RegisterInputNode)Inputs[0];

    public override string ToString()
    {
        return $"consumeSlot({Buffer})";
    }
}

/// <summary>
/// What an imm_atomic_alloc leaves in a register: the slot it took, which the
/// store_structured addressed by it then fills. The two together are one Append,
/// and an append buffer has no other spelling - no subscript, and no counter to
/// read the slot out of - so nothing else reads this and the assignment goes away
/// as dead, the way a consume's does.
/// </summary>
public class AppendSlotNode : HlslTreeNode
{
    public AppendSlotNode(RegisterInputNode buffer)
    {
        AddInput(buffer);
    }

    public RegisterInputNode Buffer => (RegisterInputNode)Inputs[0];

    public override string ToString()
    {
        return $"appendSlot({Buffer})";
    }
}

/// <summary>
/// imm_atomic_alloc or imm_atomic_consume over a buffer that keeps a counter
/// without being an append or a consume one. `buf.IncrementCounter()` takes the
/// next slot and `buf.DecrementCounter()` gives the last one back, and the slot
/// is then an index like any other - read and written through a subscript, which
/// is what an RWStructuredBuffer has and the append and consume ones have not. So
/// this is a value in its own right rather than half of a call.
/// </summary>
public class BufferCounterNode : HlslTreeNode
{
    public BufferCounterNode(RegisterInputNode buffer, bool isIncrement)
    {
        AddInput(buffer);
        IsIncrement = isIncrement;
    }

    public RegisterInputNode Buffer => (RegisterInputNode)Inputs[0];

    /// <summary>Increment for an alloc, decrement for a consume.</summary>
    public bool IsIncrement { get; }

    public override string ToString()
    {
        return $"{(IsIncrement ? "increment" : "decrement")}Counter({Buffer})";
    }
}
