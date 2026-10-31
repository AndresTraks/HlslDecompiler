namespace HlslDecompiler.DirectXShaderModel.Xenos;

// The Xenos microcode encoding, after Xenia's ucode.h (BSD license), which was
// established against XNA Game Studio 3.1's assembler and validator:
// https://github.com/xenia-project/xenia/blob/master/src/xenia/gpu/ucode.h

public enum ControlFlowOpcode
{
    Nop = 0,
    Exec = 1,
    ExecEnd = 2,
    CondExec = 3,
    CondExecEnd = 4,
    CondExecPred = 5,
    CondExecPredEnd = 6,
    LoopStart = 7,
    LoopEnd = 8,
    CondCall = 9,
    Return = 10,
    CondJmp = 11,
    Alloc = 12,
    CondExecPredClean = 13,
    CondExecPredCleanEnd = 14,
    MarkVsFetchDone = 15,
}

public enum AllocType
{
    None = 0,
    VsPosition = 1,
    // Interpolators in a vertex shader, colors in a pixel shader.
    VsInterpolatorsOrPsColors = 2,
    Memory = 3,
}

/// <summary>
/// One control flow instruction: 48 bits, two to every three dwords at the start
/// of the microcode.
/// </summary>
public readonly struct ControlFlowInstruction
{
    private readonly uint _word0;
    // The low 16 bits.
    private readonly uint _word1;

    public ControlFlowInstruction(uint word0, uint word1)
    {
        _word0 = word0;
        _word1 = word1 & 0xFFFF;
    }

    /// <summary>The pair at dwords [index * 3, index * 3 + 3).</summary>
    public static (ControlFlowInstruction, ControlFlowInstruction) Unpack(uint dword0, uint dword1, uint dword2) =>
        (new ControlFlowInstruction(dword0, dword1),
         new ControlFlowInstruction((dword1 >> 16) | (dword2 << 16), dword2 >> 16));

    public ControlFlowOpcode Opcode => (ControlFlowOpcode)((_word1 >> 12) & 0xF);

    public bool IsExec => Opcode is ControlFlowOpcode.Exec or ControlFlowOpcode.ExecEnd
        or ControlFlowOpcode.CondExec or ControlFlowOpcode.CondExecEnd
        or ControlFlowOpcode.CondExecPred or ControlFlowOpcode.CondExecPredEnd
        or ControlFlowOpcode.CondExecPredClean or ControlFlowOpcode.CondExecPredCleanEnd;

    public bool EndsShader => Opcode is ControlFlowOpcode.ExecEnd or ControlFlowOpcode.CondExecEnd
        or ControlFlowOpcode.CondExecPredEnd or ControlFlowOpcode.CondExecPredCleanEnd;

    // exec and its conditional forms.
    public int ExecAddress => (int)(_word0 & 0xFFF);
    public int ExecCount => (int)((_word0 >> 12) & 0x7);
    public bool IsYield => ((_word0 >> 15) & 1) != 0;
    /// <summary>Two bits per instruction: bit 0 fetch (not ALU), bit 1 serialize.</summary>
    public int Sequence => (int)((_word0 >> 16) & 0xFFF);
    /// <summary>exec, exece and the predicated forms; cexec with a bool constant has none.</summary>
    public bool IsPredicateClean => ((_word1 >> 9) & 1) != 0;

    // Conditional on a bool constant (cexec, ccall, cjmp) or on the predicate.
    public int BoolAddress => (int)((_word1 >> 2) & 0xFF);
    public bool Condition => ((_word1 >> 10) & 1) != 0;

    // loop, endloop, call and jmp.
    public int TargetAddress => (int)(_word0 & 0x1FFF);
    public bool IsRepeat => ((_word0 >> 13) & 1) != 0;
    public int LoopId => (int)((_word0 >> 16) & 0x1F);
    public bool IsPredicatedBreak => ((_word0 >> 21) & 1) != 0;
    public bool IsUnconditional => ((_word0 >> 13) & 1) != 0;
    public bool IsPredicated => ((_word0 >> 14) & 1) != 0;

    // alloc.
    public int AllocSize => (int)(_word0 & 0x7);
    public AllocType AllocType => (AllocType)((_word1 >> 9) & 0x3);
}

public enum FetchOpcode
{
    VertexFetch = 0,
    TextureFetch = 1,
    GetTextureBorderColorFrac = 16,
    GetTextureComputedLod = 17,
    GetTextureGradients = 18,
    GetTextureWeights = 19,
    SetTextureLod = 24,
    SetTextureGradientsHorz = 25,
    SetTextureGradientsVert = 26,
}

public enum FetchDimension
{
    D1 = 0,
    D2 = 1,
    D3OrStacked = 2,
    Cube = 3,
}

/// <summary>A vertex or texture fetch: three dwords.</summary>
public readonly struct FetchInstruction
{
    private readonly uint _d0, _d1, _d2;

    public FetchInstruction(uint d0, uint d1, uint d2)
    {
        _d0 = d0;
        _d1 = d1;
        _d2 = d2;
    }

    public FetchOpcode Opcode => (FetchOpcode)(_d0 & 0x1F);
    public int Source => (int)((_d0 >> 5) & 0x3F);
    public bool IsSourceRelative => ((_d0 >> 11) & 1) != 0;
    public int Destination => (int)((_d0 >> 12) & 0x3F);
    public bool IsDestinationRelative => ((_d0 >> 18) & 1) != 0;
    /// <summary>Three bits per component: x, y, z, w, 0, 1, or 7 to keep.</summary>
    public int DestinationSwizzle => (int)(_d1 & 0xFFF);
    public bool IsPredicated => ((_d1 >> 31) & 1) != 0;
    public bool PredicateCondition => ((_d2 >> 31) & 1) != 0;

    // Vertex fetch.
    /// <summary>vf0 to vf95: three per 32 constant slots.</summary>
    public int VertexFetchConstant => (int)((_d0 >> 20) & 0x1F) * 3 + (int)((_d0 >> 25) & 0x3);
    public int PrefetchCount => (int)((_d0 >> 27) & 0x7);
    public int VertexSourceSwizzle => (int)((_d0 >> 30) & 0x3);
    public bool IsSigned => ((_d1 >> 12) & 1) != 0;
    public bool IsInteger => ((_d1 >> 13) & 1) != 0;
    public bool IsIndexRounded => ((_d1 >> 15) & 1) != 0;
    public int DataFormat => (int)((_d1 >> 16) & 0x3F);
    public int ExpAdjust => SignExtend(_d1 >> 24, 6);
    public bool IsMiniFetch => ((_d1 >> 30) & 1) != 0;
    public int Stride => (int)(_d2 & 0xFF);
    public int Offset => SignExtend(_d2 >> 8, 23);

    // Texture fetch.
    public int TextureFetchConstant => (int)((_d0 >> 20) & 0x1F);
    public bool FetchValidOnly => ((_d0 >> 19) & 1) != 0;
    public bool UnnormalizedCoordinates => ((_d0 >> 25) & 1) != 0;
    /// <summary>Two bits per component, absolute.</summary>
    public int TextureSourceSwizzle => (int)((_d0 >> 26) & 0x3F);
    public int MagFilter => (int)((_d1 >> 12) & 0x3);
    public int MinFilter => (int)((_d1 >> 14) & 0x3);
    public int MipFilter => (int)((_d1 >> 16) & 0x3);
    public int AnisoFilter => (int)((_d1 >> 18) & 0x7);
    public int VolMagFilter => (int)((_d1 >> 24) & 0x3);
    public int VolMinFilter => (int)((_d1 >> 26) & 0x3);
    public bool UseComputedLod => ((_d1 >> 28) & 1) != 0;
    public bool UseRegisterLod => ((_d1 >> 29) & 1) != 0;
    public bool UseRegisterGradients => (_d2 & 1) != 0;
    /// <summary>In sixteenths.</summary>
    public int LodBias => SignExtend(_d2 >> 2, 7);
    public FetchDimension Dimension => (FetchDimension)((_d2 >> 14) & 0x3);
    /// <summary>In halves.</summary>
    public int OffsetX => SignExtend(_d2 >> 16, 5);
    public int OffsetY => SignExtend(_d2 >> 21, 5);
    public int OffsetZ => SignExtend(_d2 >> 26, 5);

    private static int SignExtend(uint value, int bits) => (int)(value << (32 - bits)) >> (32 - bits);
}

public enum AluVectorOpcode
{
    Add = 0, Mul = 1, Max = 2, Min = 3, Seq = 4, Sgt = 5, Sge = 6, Sne = 7,
    Frc = 8, Trunc = 9, Floor = 10, Mad = 11, CndEq = 12, CndGe = 13, CndGt = 14,
    Dp4 = 15, Dp3 = 16, Dp2Add = 17, Cube = 18, Max4 = 19,
    SetpEqPush = 20, SetpNePush = 21, SetpGtPush = 22, SetpGePush = 23,
    KillEq = 24, KillGt = 25, KillGe = 26, KillNe = 27, Dst = 28, MaxA = 29,
}

public enum AluScalarOpcode
{
    Adds = 0, AddsPrev = 1, Muls = 2, MulsPrev = 3, MulsPrev2 = 4, Maxs = 5, Mins = 6,
    Seqs = 7, Sgts = 8, Sges = 9, Snes = 10, Frcs = 11, Truncs = 12, Floors = 13,
    Exp = 14, Logc = 15, Log = 16, Rcpc = 17, Rcpf = 18, Rcp = 19, Rsqc = 20, Rsqf = 21, Rsq = 22,
    MaxAs = 23, MaxAsf = 24, Subs = 25, SubsPrev = 26,
    SetpEq = 27, SetpNe = 28, SetpGt = 29, SetpGe = 30, SetpInv = 31, SetpPop = 32, SetpClr = 33, SetpRstr = 34,
    KillsEq = 35, KillsGt = 36, KillsGe = 37, KillsNe = 38, KillsOne = 39, Sqrt = 40,
    Mulsc0 = 42, Mulsc1 = 43, Addsc0 = 44, Addsc1 = 45, Subsc0 = 46, Subsc1 = 47,
    Sin = 48, Cos = 49, RetainPrev = 50,
}

/// <summary>
/// A co-issued vector and scalar operation: three dwords. Operand 3 is the
/// scalar operation's, and the vector operation's third when it has one.
/// </summary>
public readonly struct AluInstruction
{
    private readonly uint _d0, _d1, _d2;

    public AluInstruction(uint d0, uint d1, uint d2)
    {
        _d0 = d0;
        _d1 = d1;
        _d2 = d2;
    }

    public int VectorDestination => (int)(_d0 & 0x3F);
    public bool IsVectorDestinationRelative => ((_d0 >> 6) & 1) != 0;
    public bool AbsConstants => ((_d0 >> 7) & 1) != 0;
    public int ScalarDestination => (int)((_d0 >> 8) & 0x3F);
    public bool IsScalarDestinationRelative => ((_d0 >> 14) & 1) != 0;
    /// <summary>Both operations write the export named by the vector destination.</summary>
    public bool IsExport => ((_d0 >> 15) & 1) != 0;
    public int VectorWriteMask => (int)((_d0 >> 16) & 0xF);
    public int ScalarWriteMask => (int)((_d0 >> 20) & 0xF);
    public bool VectorClamp => ((_d0 >> 24) & 1) != 0;
    public bool ScalarClamp => ((_d0 >> 25) & 1) != 0;
    public AluScalarOpcode ScalarOpcode => (AluScalarOpcode)((_d0 >> 26) & 0x3F);

    /// <summary>Component-relative: each two bits are added to the component's index.</summary>
    public int SourceSwizzle(int i) => (int)((_d1 >> (8 * (3 - i))) & 0xFF);
    public bool SourceNegate(int i) => ((_d1 >> (27 - i)) & 1) != 0;
    public bool PredicateCondition => ((_d1 >> 27) & 1) != 0;
    public bool IsPredicated => ((_d1 >> 28) & 1) != 0;
    public bool IsConstantAddressRegisterRelative => ((_d1 >> 29) & 1) != 0;
    private bool IsConstant1Relative => ((_d1 >> 30) & 1) != 0;
    private bool IsConstant0Relative => ((_d1 >> 31) & 1) != 0;

    /// <summary>A constant's index, or a temp's packed index, aL bit (0x40) and absolute bit (0x80).</summary>
    public int SourceRegister(int i) => (int)((_d2 >> (8 * (3 - i))) & 0xFF);
    public AluVectorOpcode VectorOpcode => (AluVectorOpcode)((_d2 >> 24) & 0x1F);
    public bool SourceIsTemp(int i) => ((_d2 >> (32 - i)) & 1) != 0;

    /// <summary>
    /// The first constant operand takes const_0_rel_abs, any later one
    /// const_1_rel_abs (the validator requires the second and third to match).
    /// </summary>
    public bool IsSourceConstantRelative(int i) => i switch
    {
        1 => IsConstant0Relative,
        2 => SourceIsTemp(1) ? IsConstant0Relative : IsConstant1Relative,
        _ => SourceIsTemp(1) && SourceIsTemp(2) ? IsConstant0Relative : IsConstant1Relative,
    };

    /// <summary>mulsc, addsc and subsc's temp: one bit in the opcode, one in src3_sel, four in src3's swizzle.</summary>
    public int ScalarConstantOperationTemp =>
        ((int)ScalarOpcode & 1) | ((SourceIsTemp(3) ? 1 : 0) << 1) | (SourceSwizzle(3) & 0x3C);
}
