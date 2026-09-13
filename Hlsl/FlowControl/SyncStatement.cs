using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// A memory barrier, GroupMemoryBarrierWithGroupSync() and its kin. A statement
/// of its own so that it closes the assignment statement around it: a groupshared
/// load before it and one after it read different memory, and neither may be
/// moved across it.
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

    // The intrinsic the flags spell. fxc emits a fixed set of combinations, one
    // per intrinsic; anything else is not a barrier HLSL can write.
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
}
