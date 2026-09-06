using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

// A run of def-defined constants that is read through a relative address, which is
// what `static const float4 palette[4]` indexed by a variable compiles to. The
// constant table describes uniforms and says nothing about defs, so nothing names
// it and nothing bounds it: the name comes from the register the run starts at and
// the length from however many defs sit next to it.
public sealed class ConstantArray
{
    public ConstantArray(int baseRegisterIndex, IReadOnlyList<ConstantRegister> registers)
    {
        BaseRegisterIndex = baseRegisterIndex;
        Registers = registers;
    }

    public int BaseRegisterIndex { get; }
    public IReadOnlyList<ConstantRegister> Registers { get; }

    public string Name => $"c{BaseRegisterIndex}";

    public bool Contains(int registerIndex)
    {
        return registerIndex >= BaseRegisterIndex
            && registerIndex < BaseRegisterIndex + Registers.Count;
    }
}
