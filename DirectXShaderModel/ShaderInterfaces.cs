using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// What the IFCE chunk says about dynamic linkage: the classes the shader was
/// compiled with, by name, and for each run of interface slots which class fills
/// which function table. The instructions only number the tables; this is the one
/// place that says ft1 and ft3 are both class A's.
/// </summary>
public class ShaderInterfaces
{
    /// <summary>The class types, indexed by type ID.</summary>
    public IList<string> ClassTypeNames { get; } = [];

    public int ClassInstanceCount { get; init; }

    public IList<InterfaceSlotRecord> SlotRecords { get; } = [];

    /// <summary>
    /// The record covering a slot. Records cover the slots in order, each as many
    /// as its span: an array of three interfaces is one record spanning three.
    /// </summary>
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

/// <summary>
/// One run of interface slots: the class type IDs that can be bound there, and
/// beside each the function table that class fills.
/// </summary>
public class InterfaceSlotRecord
{
    public int SlotSpan { get; init; }
    public int[] TypeIds { get; init; }
    public int[] TableIds { get; init; }
}
