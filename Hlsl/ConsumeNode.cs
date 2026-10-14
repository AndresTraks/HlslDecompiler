namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of `input.Consume()`, compiled as imm_atomic_consume plus one
/// ld_structured per component. The call has no other spelling, so it is named
/// and its components read from the variable, like a GetDimensions result.
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
    /// Identifies the call. Held, not an input, so the consume's slot assignment
    /// becomes dead.
    /// </summary>
    public HlslTreeNode Slot { get; }

    public TempVariableNode NamedAs { get; set; }

    // From the buffer's declared element type; the slot itself is always an integer.
    public bool? IsIntegerElement { get; init; }

    public override string ToString()
    {
        return $"consume({Buffer}).{"xyzw"[ComponentIndex]}";
    }
}

/// <summary>
/// The slot imm_atomic_consume returns. Unread once the loads are <see
/// cref="ConsumeNode"/>s, so its assignment dies.
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
/// The slot imm_atomic_alloc returns, filled by a store_structured; together one
/// Append. Nothing else reads it, so its assignment dies.
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
/// imm_atomic_alloc/consume on a counter buffer that is not append/consume:
/// `buf.IncrementCounter()` or `buf.DecrementCounter()`, a value used as an index.
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
