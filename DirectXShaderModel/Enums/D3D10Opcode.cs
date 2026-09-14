using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

// D3D10_SB_OPCODE_TYPE
public enum D3D10Opcode
{
    Add,
    And,
    Break,
    BreakC,
    Call,
    CallC,
    Case,
    Continue,
    ContinueC,
    Cut,
    Default,
    DerivRtx,
    DerivRty,
    Discard,
    Div,
    Dp2,
    Dp3,
    Dp4,
    Else,
    Emit,
    EmitThenCut,
    EndIf,
    EndLoop,
    EndSwitch,
    Eq,
    Exp,
    Frc,
    Ftoi,
    Ftou,
    GE,
    IAdd,
    If,
    Ieq,
    Ige,
    Ilt,
    IMad,
    IMax,
    IMin,
    IMul,
    Ine,
    INeg,
    IShl,
    IShr,
    IToF,
    Label,
    LD,
    LDMS,
    Log,
    Loop,
    LT,
    Mad,
    Min,
    Max,
    CustomData,
    Mov,
    MovC,
    Mul,
    Ne,
    Nop,
    Not,
    Or,
    ResInfo,
    Ret,
    RetC,
    RoundNe,
    RoundNi,
    RoundPi,
    RoundZ,
    Rsq,
    Sample,
    SampleC,
    SampleCLZ,
    SampleL,
    SampleD,
    SampleB,
    Sqrt,
    Swtich,
    SinCos,
    Udiv,
    ULT,
    UGE,
    UMul,
    Umad,
    UMax,
    UMin,
    UShr,
    UTof,
    Xor,
    DclResource,
    DclConstantBuffer,
    DclSampler,
    DclIndexRange,
    DclGSOutputPrimitiveTopology,
    DclGSInputPrimitive,
    DclGSMaxOutputVertexCount,
    DclInput,
    DclInputSgv,
    DclInputSiv,
    DclInputPS,
    DclInputPSSgv,
    DclInputPSSiv,
    DclOutput,
    DclOutputSgv,
    DclOutputSiv,
    DclTemps,
    DclIndexableTemp,
    DclGlobalFlags,
    Reserved0,
    Lod,
    Gather4,
    SamplePos,
    SampleInfo,
    Reserved1,
    HsDecls,
    HsControlPointPhase,
    HsForkPhase,
    HsJoinPhase,
    EmitStream,
    CutStream,
    EmitThenCutStream,
    InterfaceCall,
    BufInfo,
    DerivRtxCoarse,
    DerivRtxFine,
    DerivRtyCoarse,
    DerivRtyFine,
    Gather4C,
    Gather4Po,
    Gather4PoC,
    Rcp,
    F32ToF16,
    F16ToF32,
    UAddC,
    USubB,
    CountBits,
    FirstBitHi,
    FirstBitLo,
    FirstBitSHi,
    UBFE,
    IBFE,
    BFI,
    BFRev,
    SwapC,
    DclStream,
    DclFunctionBody,
    DclFunctionTable,
    DclInterface,
    DclInputControlPointCount,
    DclOutputControlPointCount,
    DclTessDomain,
    DclTessPartitioning,
    DclTessOutputPrimitive,
    DclHSMaxTessFactor,
    DclHSForkPhaseInstanceCount,
    DclHSJoinPhaseInstanceCount,
    DclThreadGroup,
    DclUnorderedAccessViewTyped,
    DclUnorderedAccessViewRaw,
    DclUnorderedAccessViewStructured,
    DclThreadGroupSharedMemoryRaw,
    DclThreadGroupSharedMemoryStructured,
    DclResourceRaw,
    DclResourceStructured,
    LdUAVTyped,
    StoreUAVTyped,
    LdRaw,
    StoreRaw,
    LdStructured,
    StoreStructured,
    AtomicAnd,
    AtomicOr,
    AtomicXor,
    AtomicCmpStore,
    AtomicIAdd,
    AtomicIMax,
    AtomicIMin,
    AtomicUMax,
    AtomicUMin,
    ImmAtomicAlloc,
    ImmAtomicConsume,
    ImmAtomicIAdd,
    ImmAtomicAnd,
    ImmAtomicOr,
    ImmAtomicXor,
    ImmAtomicExch,
    ImmAtomicCmpExch,
    ImmAtomicIMax,
    ImmAtomicIMin,
    ImmAtomicUMax,
    ImmAtomicUMin,
    Sync,
    DAdd,
    DMax,
    DMin,
    DMul,
    DEq,
    DGe,
    DLt,
    DNe,
    DMov,
    DMovC,
    DToF,
    FToD,
    EvalSnapped,
    EvalSampleIndex,
    EvalCentroid,
    DclGSInstanceCount,
    Abort,
    DebugBreak,
    D3D11Reserved0,
    DDiv,
    DFMA,
    DRCP,
    MSAD,
    DToI,
    DToU,
    IToD,
    UToD,
    D3d111Reserved0
}

public static class D3D10OpcodeExtensions
{
    // Declaration tokens use bits 11 and up for their own fields - interpolation
    // mode, primitive type and so on - so flags defined on instruction tokens, the
    // saturate modifier in particular, must not be read out of them.
    private static readonly HashSet<D3D10Opcode> Declarations =
        [.. Enum.GetValues<D3D10Opcode>().Where(o => o.ToString().StartsWith("Dcl"))];

    public static bool IsDeclaration(this D3D10Opcode opcode)
    {
        return Declarations.Contains(opcode);
    }

    // Those whose opcode token carries the test-boolean bit, saying whether they
    // branch on the register being non-zero or on its being zero.
    public static bool HasBooleanTest(this D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.If:
            case D3D10Opcode.BreakC:
            case D3D10Opcode.ContinueC:
            case D3D10Opcode.RetC:
            case D3D10Opcode.CallC:
            case D3D10Opcode.Discard:
                return true;
            default:
                return false;
        }
    }

    public static bool IsInteger(this D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.Case:
            // Its only immediate operand is the texel address, which is integer.
            case D3D10Opcode.LD:
            case D3D10Opcode.LDMS:
            case D3D10Opcode.IAdd:
            case D3D10Opcode.IShl:
            case D3D10Opcode.IShr:
            case D3D10Opcode.UShr:
            case D3D10Opcode.Ieq:
            case D3D10Opcode.Ige:
            case D3D10Opcode.UGE:
            case D3D10Opcode.ULT:
            case D3D10Opcode.Ilt:
            case D3D10Opcode.IMad:
            case D3D10Opcode.IMin:
            case D3D10Opcode.IMul:
            // The unsigned arithmetic is as integer as the signed. Leaving udiv out
            // printed its divisor l(3, 3, 0, 0) as floats, where the bits of the
            // integer 3 are a denormal and the division is by zero.
            case D3D10Opcode.Udiv:
            case D3D10Opcode.Umad:
            case D3D10Opcode.UMax:
            case D3D10Opcode.UMin:
            case D3D10Opcode.UMul:
            case D3D10Opcode.Ine:
            case D3D10Opcode.INeg:
            case D3D10Opcode.IMax:
            case D3D10Opcode.Discard:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// What an instruction reads its register operands as. The integer
    /// instructions and itof read integers; ftoi and the float instructions read
    /// floats; a branch, a discard and a movc condition test bits; and a mov, a
    /// movc value or a bitwise operator carries whatever it was given.
    /// </summary>
    public static ValueKind ConsumedKind(this D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.Mov:
            case D3D10Opcode.MovC:
            case D3D10Opcode.And:
            case D3D10Opcode.Or:
            case D3D10Opcode.Xor:
            case D3D10Opcode.Not:
                return ValueKind.Unknown;
            case D3D10Opcode.If:
            case D3D10Opcode.BreakC:
            case D3D10Opcode.ContinueC:
            case D3D10Opcode.RetC:
            case D3D10Opcode.CallC:
            case D3D10Opcode.Discard:
                return ValueKind.Bits;
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            // The mip level, and the element and byte offsets.
            case D3D10Opcode.ResInfo:
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.LdRaw:
            case D3D10Opcode.StoreRaw:
            case D3D10Opcode.Swtich:
                return ValueKind.Integer;
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
                return ValueKind.Float;
            default:
                return opcode.IsInteger() ? ValueKind.Integer : ValueKind.Float;
        }
    }

    /// <summary>
    /// What an instruction writes. A comparison writes a mask, all ones or all
    /// zeroes; ftoi an integer, itof a float; a mov, a movc and the bitwise
    /// operators write what they read. resinfo and ld_structured depend on the
    /// instruction rather than the opcode and are answered elsewhere.
    /// </summary>
    public static ValueKind ProducedKind(this D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.Mov:
            case D3D10Opcode.MovC:
            case D3D10Opcode.And:
            case D3D10Opcode.Or:
            case D3D10Opcode.Xor:
            case D3D10Opcode.Not:
            case D3D10Opcode.ResInfo:
            case D3D10Opcode.LdStructured:
                return ValueKind.Unknown;
            // A raw buffer holds dwords, read out as uints.
            case D3D10Opcode.LdRaw:
                return ValueKind.Integer;
            case D3D10Opcode.LT:
            case D3D10Opcode.GE:
            case D3D10Opcode.Eq:
            case D3D10Opcode.Ne:
            case D3D10Opcode.Ilt:
            case D3D10Opcode.Ige:
            case D3D10Opcode.Ieq:
            case D3D10Opcode.Ine:
            case D3D10Opcode.ULT:
            case D3D10Opcode.UGE:
                return ValueKind.Bits;
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
                return ValueKind.Integer;
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            // A texel, whatever address it was read at.
            case D3D10Opcode.LD:
            case D3D10Opcode.LDMS:
                return ValueKind.Float;
            default:
                return opcode.IsInteger() ? ValueKind.Integer : ValueKind.Float;
        }
    }
}
