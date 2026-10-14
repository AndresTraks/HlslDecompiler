using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

// A run of def constants read through a relative address: a `static const`
// array. Named after its start register; its length is the run of adjacent defs.
public sealed class ConstantArray
{
    public ConstantArray(int baseRegisterIndex, IReadOnlyList<ConstantRegister> registers, string namePrefix = "")
    {
        BaseRegisterIndex = baseRegisterIndex;
        Registers = registers;
        NamePrefix = namePrefix;
    }

    public int BaseRegisterIndex { get; }
    public IReadOnlyList<ConstantRegister> Registers { get; }

    /// <summary>See <see cref="RegisterState.LocalNamePrefix"/>.</summary>
    public string NamePrefix { get; }

    public string Name => $"{NamePrefix}c{BaseRegisterIndex}";

    public bool Contains(int registerIndex)
    {
        return registerIndex >= BaseRegisterIndex
            && registerIndex < BaseRegisterIndex + Registers.Count;
    }
}
