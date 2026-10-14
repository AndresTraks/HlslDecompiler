using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// The IFCE chunk: class names, and per run of interface slots which class fills
/// which function table. The only place linking tables to classes.
/// </summary>
public class ShaderInterfaces
{
    /// <summary>The class types, indexed by type ID.</summary>
    public IList<string> ClassTypeNames { get; } = [];

    public int ClassInstanceCount { get; init; }

    public IList<InterfaceSlotRecord> SlotRecords { get; } = [];

    /// <summary>Records cover slots in order, each SlotSpan of them.</summary>
    public InterfaceSlotRecord RecordForSlot(int slot)
    {
        int first = 0;
        foreach (InterfaceSlotRecord record in SlotRecords)
        {
            if (slot < first + record.SlotSpan)
            {
                return record;
            }
            first += record.SlotSpan;
        }
        return null;
    }
}

/// <summary>Bindable class type IDs, each with the function table it fills.</summary>
public class InterfaceSlotRecord
{
    public int SlotSpan { get; init; }
    public int[] TypeIds { get; init; }
    public int[] TableIds { get; init; }
}
