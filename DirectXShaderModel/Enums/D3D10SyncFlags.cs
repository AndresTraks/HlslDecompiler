using System;

namespace HlslDecompiler.DirectXShaderModel;

// Bits 11-14 of the sync opcode token.
[Flags]
public enum D3D10SyncFlags
{
    None = 0,
    ThreadsInGroup = 1,
    ThreadGroupSharedMemory = 2,
    UavMemoryGroup = 4,
    UavMemoryGlobal = 8,
}
