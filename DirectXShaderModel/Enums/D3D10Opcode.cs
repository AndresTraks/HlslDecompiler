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

    /// <summary>
    /// The interlocked operations that keep no result. The ones that do -
    /// imm_atomic_iadd and its family - name a destination register as well and are
    /// not read yet.
    /// </summary>
    public static bool IsAtomic(this D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.AtomicIAdd:
            case D3D10Opcode.AtomicAnd:
            case D3D10Opcode.AtomicOr:
            case D3D10Opcode.AtomicXor:
            case D3D10Opcode.AtomicIMax:
            case D3D10Opcode.AtomicIMin:
            case D3D10Opcode.AtomicUMax:
            case D3D10Opcode.AtomicUMin:
            case D3D10Opcode.AtomicCmpStore:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Whether the interlocked operation keeps the value the resource held before
    /// it. The same operations as above with an imm_ in front and a destination
    /// register in front of the resource, which HLSL writes as the out parameter
    /// the two argument form leaves off.
    /// </summary>
    public static bool IsImmediateAtomic(this D3D10Opcode opcode)
    {
        switch (opcode)
        {
            case D3D10Opcode.ImmAtomicAlloc:
            case D3D10Opcode.ImmAtomicConsume:
            case D3D10Opcode.ImmAtomicIAdd:
            case D3D10Opcode.ImmAtomicAnd:
            case D3D10Opcode.ImmAtomicOr:
            case D3D10Opcode.ImmAtomicXor:
            case D3D10Opcode.ImmAtomicIMax:
            case D3D10Opcode.ImmAtomicIMin:
            case D3D10Opcode.ImmAtomicUMax:
            case D3D10Opcode.ImmAtomicUMin:
            case D3D10Opcode.ImmAtomicExch:
            case D3D10Opcode.ImmAtomicCmpExch:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The HLSL intrinsic an interlocked operation is written as. The unsigned and
    /// the signed minimum and maximum share one name each - the operands say which,
    /// the way they do for umin and imin themselves.
    /// </summary>
    public static string AtomicMethodName(this D3D10Opcode opcode)
    {
        return opcode switch
        {
            D3D10Opcode.AtomicIAdd => "InterlockedAdd",
            D3D10Opcode.AtomicAnd => "InterlockedAnd",
            D3D10Opcode.AtomicOr => "InterlockedOr",
            D3D10Opcode.AtomicXor => "InterlockedXor",
            D3D10Opcode.AtomicIMax or D3D10Opcode.AtomicUMax => "InterlockedMax",
            D3D10Opcode.AtomicIMin or D3D10Opcode.AtomicUMin => "InterlockedMin",
            D3D10Opcode.AtomicCmpStore => "InterlockedCompareStore",
            D3D10Opcode.ImmAtomicIAdd => "InterlockedAdd",
            D3D10Opcode.ImmAtomicAnd => "InterlockedAnd",
            D3D10Opcode.ImmAtomicOr => "InterlockedOr",
            D3D10Opcode.ImmAtomicXor => "InterlockedXor",
            D3D10Opcode.ImmAtomicIMax or D3D10Opcode.ImmAtomicUMax => "InterlockedMax",
            D3D10Opcode.ImmAtomicIMin or D3D10Opcode.ImmAtomicUMin => "InterlockedMin",
            // The exchanges have no form that drops the old value, so these two
            // names belong to the imm_ opcodes alone.
            D3D10Opcode.ImmAtomicExch => "InterlockedExchange",
            D3D10Opcode.ImmAtomicCmpExch => "InterlockedCompareExchange",
            _ => throw new NotImplementedException(opcode.ToString()),
        };
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
            // Every operand of an interlocked operation is an integer - the address,
            // the value and the one it compares against - since there is no atomic
            // over anything else.
            case D3D10Opcode.AtomicIAdd:
            case D3D10Opcode.AtomicAnd:
            case D3D10Opcode.AtomicOr:
            case D3D10Opcode.AtomicXor:
            case D3D10Opcode.AtomicIMax:
            case D3D10Opcode.AtomicIMin:
            case D3D10Opcode.AtomicUMax:
            case D3D10Opcode.AtomicUMin:
            case D3D10Opcode.AtomicCmpStore:
            case D3D10Opcode.ImmAtomicIAdd:
            case D3D10Opcode.ImmAtomicAnd:
            case D3D10Opcode.ImmAtomicOr:
            case D3D10Opcode.ImmAtomicXor:
            case D3D10Opcode.ImmAtomicIMax:
            case D3D10Opcode.ImmAtomicIMin:
            case D3D10Opcode.ImmAtomicUMax:
            case D3D10Opcode.ImmAtomicUMin:
            case D3D10Opcode.ImmAtomicExch:
            case D3D10Opcode.ImmAtomicCmpExch:
            // How many elements a buffer holds is a count, and so is the register
            // it lands in.
            case D3D10Opcode.BufInfo:
            // The bit instructions read an integer and count or reorder its bits.
            case D3D10Opcode.CountBits:
            case D3D10Opcode.FirstBitLo:
            case D3D10Opcode.FirstBitHi:
            case D3D10Opcode.FirstBitSHi:
            case D3D10Opcode.BFRev:
            case D3D10Opcode.F16ToF32:
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
            case D3D10Opcode.BufInfo:
            case D3D10Opcode.LdStructured:
            case D3D10Opcode.LdRaw:
            case D3D10Opcode.StoreRaw:
            case D3D10Opcode.Swtich:
                return ValueKind.Integer;
            case D3D10Opcode.Ftoi:
            case D3D10Opcode.Ftou:
            // The float it takes the bits of the nearest half float to.
            case D3D10Opcode.F32ToF16:
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
            // How many elements a buffer holds, which is a count and not a measure
            // of anything that could be a float.
            case D3D10Opcode.BufInfo:
                return ValueKind.Integer;
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
            // Half float bits, which are an integer however they got there.
            case D3D10Opcode.F32ToF16:
                return ValueKind.Integer;
            case D3D10Opcode.IToF:
            case D3D10Opcode.UTof:
            case D3D10Opcode.F16ToF32:
            // A texel, whatever address it was read at.
            case D3D10Opcode.LD:
            case D3D10Opcode.LDMS:
                return ValueKind.Float;
            default:
                return opcode.IsInteger() ? ValueKind.Integer : ValueKind.Float;
        }
    }
}
