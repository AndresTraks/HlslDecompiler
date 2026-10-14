using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// A memory barrier. A statement of its own so that no load is moved across it.
/// </summary>
public class SyncStatement : IStatement
{
    public D3D10SyncFlags Flags { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
    public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

    public SyncStatement(D3D10SyncFlags flags, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
    {
        Flags = flags;
        Inputs = inputs.ToDictionary();
        Outputs = inputs.ToDictionary();
    }

    public string IntrinsicName => GetIntrinsicName(Flags);

    // fxc emits one fixed flag combination per intrinsic; anything else has no HLSL.
    public static string GetIntrinsicName(D3D10SyncFlags flags)
    {
        bool threads = flags.HasFlag(D3D10SyncFlags.ThreadsInGroup);
        bool shared = flags.HasFlag(D3D10SyncFlags.ThreadGroupSharedMemory);
        bool uav = flags.HasFlag(D3D10SyncFlags.UavMemoryGroup) || flags.HasFlag(D3D10SyncFlags.UavMemoryGlobal);
        string barrier = (shared, uav) switch
        {
            (true, false) => "GroupMemoryBarrier",
            (false, true) => "DeviceMemoryBarrier",
            (true, true) => "AllMemoryBarrier",
            _ => throw new System.NotImplementedException(flags.ToString()),
        };
        return threads ? barrier + "WithGroupSync" : barrier;
    }

    public IEnumerable<HeldSlot> HeldSlots => [];
}
