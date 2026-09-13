using System;

namespace HlslDecompiler.DirectXShaderModel;

// What a sync instruction waits on, from bits 11-14 of its opcode token. fxc
// spells them as suffixes, sync_uglobal_g_t, in this order.
[Flags]
public enum D3D10SyncFlags
{
    None = 0,
    ThreadsInGroup = 1,
    ThreadGroupSharedMemory = 2,
    UavMemoryGroup = 4,
    UavMemoryGlobal = 8,
}
