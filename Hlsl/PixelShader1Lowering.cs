using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Rewrites a ps_1_1 to ps_1_4 pixel shader as an equivalent ps_2_0 one, which the
/// writers know how to decompile.
///
/// ps_1_1 to ps_1_3:
/// - t# is both coordinate and result (`tex t0`, `texcoord t1` saturated, alpha 1);
///   each written t# becomes a stand-in temp r8 to r11. texkill reads the coordinate.
/// - No declarations: v0/v1 are diffuse/specular, stage # samples sampler #, 2D
///   unless the constant table says otherwise.
/// - Source modifiers (_bias, _bx2, 1-x, negated) become instructions into scratch
///   temps r2 to r6; result scales (_x2 to _d8) apply before the saturate.
/// - sub is an add of the negation; cnd (first > 0.5) becomes cmp on 0.5 - x with
///   the other two sources swapped.
/// - A co-issued pair sees neither's writes: the first is held in r7 if needed.
/// - r0 is the returned colour.
///
/// ps_1_4 keeps modifiers, scales, cnd and co-issue, but:
/// - t# is a read-only coordinate; r0 to r5 are temps, scratch is r6 to r10, the
///   co-issue hold r11.
/// - `texld r#, src` samples stage #; a swizzled (.xyw) or projected (_dz, _dw)
///   coordinate is computed into a scratch temp, since ps_2_0 reads it whole.
/// - `texcrd` is the unclamped coordinate; writing alpha, or blue when projected,
///   is undefined and refused.
/// - `texdepth r5` writes r5.r / r5.g as depth, 1 where r5.g is 0.
///
/// texbem/texbeml/bem read the bump matrix and luminance from texture stage state,
/// so a constant table of their own declares them as uniforms: bumpEnvMat# a
/// column-major float2x2 from c8, two registers a stage (offset is
/// mul(float2(du, dv), it)), and bumpEnvLum# a float2 (scale, offset) from c20.
/// Helper constants go in c29 to c31, out of reach of ps_1_x's eight.
/// </summary>
public static class PixelShader1Lowering
{
    private const int FirstTextureStandIn = 8;
    private const int ZeroConstant = 29; // (0, 0, 0, 0)
    private const int BumpMatrixBase = 8;
    private const int BumpLuminanceBase = 20;
    private const int HelperConstant = 30; // (0.5, 1, 2, -1)
    private const int ScaleConstant = 31; // (4, 8, 0.25, 0.125)

    private const byte Identity = 0xE4;
    private const byte ReplicateX = 0x00;
    private const byte ReplicateY = 0x55;
    private const byte ReplicateZ = 0xAA;
    private const byte ReplicateW = 0xFF;

    public static bool Applies(ShaderModel shader) =>
        shader.Type == ShaderType.Pixel && shader.MajorVersion == 1;

    /// <summary>The ps_2_0 rewrite of a ps_1_x shader, or the shader itself otherwise.</summary>
    public static ShaderModel Lower(ShaderModel shader)
    {
        if (!Applies(shader))
        {
            return shader;
        }
        return new Lowering(shader).Run();
    }

    private sealed class Lowering(ShaderModel shader)
    {
        private readonly List<uint[]> _comments = [];
        private readonly List<uint[]> _definitions = [];
        private readonly List<uint[]> _body = [];
        private readonly Dictionary<int, int> _coordinateMasks = [];
        private readonly SortedSet<int> _colours = [];
        private readonly SortedDictionary<int, SamplerTextureType> _samplers = [];
        private bool _usesHelper;
        private bool _usesScale;
        private bool _usesZero;
        private int _nextScratch;
        private readonly SortedSet<int> _bumpMatrices = [];
        private readonly SortedSet<int> _bumpLuminances = [];

        // A texm3x2/texm3x3 under way: its first pad's stage (whose stand-in collects
        // the rows) and rows so far.
        private int _matrixBase = -1;
        private int _matrixRows;

        // ps_1_4 has six temps; earlier versions have two.
        private bool IsVersion14 => shader.MinorVersion >= 4;
        private int FirstScratch => IsVersion14 ? 6 : 2;
        private int LastScratch => IsVersion14 ? 10 : 6;
        private int CoIssueHold => IsVersion14 ? 11 : 7;

        public ShaderModel Run()
        {
            List<D3D9Instruction> instructions = [.. shader.Instructions.OfType<D3D9Instruction>()];
            for (int i = 0; i < instructions.Count; i++)
            {
                D3D9Instruction instruction = instructions[i];
                D3D9Instruction coIssued = i + 1 < instructions.Count && instructions[i + 1].CoIssue
                    ? instructions[i + 1]
                    : null;
                if (coIssued != null && ReadsRegisterWrittenBy(coIssued, instruction))
                {
                    // The second must not see the first's write: hold it aside.
                    uint destination = Destination(instruction);
                    Lower(instruction, holdIn: CoIssueHold);
                    Lower(coIssued, holdIn: null);
                    Emit(Opcode.Mov, ClearResultModifiers(destination),
                        Source(RegisterType.Temp, CoIssueHold, Identity));
                    i++;
                    continue;
                }
                Lower(instruction, holdIn: null);
            }

            Emit(Opcode.Mov, DestinationToken(RegisterType.ColorOut, 0, 0xF),
                Source(RegisterType.Temp, 0, Identity));

            return Assemble();
        }

        private void Lower(D3D9Instruction instruction, int? holdIn)
        {
            _nextScratch = FirstScratch;
            switch (instruction.Opcode)
            {
                case Opcode.Comment:
                    _comments.Add([instruction.InstructionToken, .. ParamTokens(instruction)]);
                    return;
                case Opcode.Def:
                    _definitions.Add([.. ParamTokens(instruction)]);
                    return;
                case Opcode.End:
                case Opcode.Nop:
                case Opcode.Phase:
                    return;
                case Opcode.Tex when IsVersion14:
                    LowerTexld(instruction);
                    return;
                case Opcode.Tex:
                    LowerTex(instruction);
                    return;
                case Opcode.TexCoord when IsVersion14:
                    LowerTexcrd(instruction);
                    return;
                case Opcode.TexCoord:
                    LowerTexCoord(instruction);
                    return;
                case Opcode.TexDepth when IsVersion14:
                    LowerTexDepth(instruction);
                    return;
                case Opcode.TexKill when IsVersion14:
                    {
                        RegisterType type = instruction.GetParamRegisterType(0);
                        int number = instruction.GetParamRegisterNumber(0);
                        if (type == RegisterType.Texture)
                        {
                            UseCoordinate(number, 0x7);
                        }
                        Emit(Opcode.TexKill, DestinationToken(type, number, 0x7));
                        return;
                    }
                case Opcode.TexKill:
                    {
                        // The coordinate, not the stand-in a tex wrote.
                        int stage = instruction.GetParamRegisterNumber(0);
                        UseCoordinate(stage, 0x7);
                        Emit(Opcode.TexKill, DestinationToken(RegisterType.Texture, stage, 0x7));
                        return;
                    }
                case Opcode.Add:
                case Opcode.Sub:
                case Opcode.Mul:
                case Opcode.Mad:
                case Opcode.Lrp:
                case Opcode.Mov:
                case Opcode.Dp3:
                case Opcode.Dp4:
                case Opcode.Cmp:
                case Opcode.Cnd:
                    LowerArithmetic(instruction, holdIn);
                    return;
                case Opcode.Bem when IsVersion14 && holdIn == null:
                    LowerBem(instruction);
                    return;
                case Opcode.TexBem when !IsVersion14:
                case Opcode.TexBeml when !IsVersion14:
                    LowerTexBem(instruction);
                    return;
                case Opcode.TexReg2AR when !IsVersion14:
                    LowerTexReg2(instruction, 0x03); // (a, r)
                    return;
                case Opcode.TexReg2GB when !IsVersion14:
                    LowerTexReg2(instruction, 0xA9); // (g, b)
                    return;
                case Opcode.TexReg2RGB when !IsVersion14:
                    LowerTexReg2(instruction, Identity);
                    return;
                case Opcode.TeXM3x2Pad when !IsVersion14:
                case Opcode.TeXM3x3Pad when !IsVersion14:
                    MatrixRow(instruction, finalRow: false,
                        instruction.Opcode == Opcode.TeXM3x2Pad ? 2 : 3);
                    return;
                case Opcode.TexM3x2Tex when !IsVersion14:
                case Opcode.TexM3x3Tex when !IsVersion14:
                    {
                        int vector = MatrixRow(instruction, finalRow: true,
                            instruction.Opcode == Opcode.TexM3x2Tex ? 2 : 3);
                        Sample(instruction.GetParamRegisterNumber(0),
                            Source(RegisterType.Temp, vector, Identity));
                        return;
                    }
                case Opcode.TexM3x2Depth when !IsVersion14:
                    EmitDepth(MatrixRow(instruction, finalRow: true, 2));
                    return;
                case Opcode.TexM3x3 when !IsVersion14:
                    {
                        int vector = MatrixRow(instruction, finalRow: true, 3);
                        int standIn = FirstTextureStandIn + instruction.GetParamRegisterNumber(0);
                        Emit(Opcode.Mov, DestinationToken(RegisterType.Temp, standIn, 0x7),
                            Source(RegisterType.Temp, vector, Identity));
                        Emit(Opcode.Mov, DestinationToken(RegisterType.Temp, standIn, 0x8),
                            Helper(ReplicateY));
                        return;
                    }
                case Opcode.TexM3x3Spec when !IsVersion14:
                case Opcode.TexM3x3VSpec when !IsVersion14:
                    LowerTexM3x3Spec(instruction);
                    return;
                case Opcode.TexDP3 when !IsVersion14:
                    Emit(Opcode.Dp3,
                        DestinationToken(RegisterType.Temp,
                            FirstTextureStandIn + instruction.GetParamRegisterNumber(0), 0xF),
                        CoordinateSet(instruction.GetParamRegisterNumber(0), 0x7),
                        TextureColour(instruction, 1, 0x7));
                    return;
                case Opcode.TexDP3Tex when !IsVersion14:
                    {
                        int stage = instruction.GetParamRegisterNumber(0);
                        int lookup = TakeScratch();
                        Emit(Opcode.Dp3, DestinationToken(RegisterType.Temp, lookup, 0x1),
                            CoordinateSet(stage, 0x7), TextureColour(instruction, 1, 0x7));
                        _usesZero = true;
                        Emit(Opcode.Mov, DestinationToken(RegisterType.Temp, lookup, 0x2),
                            Source(RegisterType.Const, ZeroConstant, ReplicateX));
                        Sample(stage, Source(RegisterType.Temp, lookup, Identity));
                        return;
                    }
                default:
                    throw new NotImplementedException(
                        $"{instruction.Opcode} in a ps_1_x pixel shader");
            }
        }

        private void LowerTex(D3D9Instruction instruction)
        {
            int stage = instruction.GetParamRegisterNumber(0);
            SamplerTextureType type = SamplerType(stage);
            _samplers[stage] = type;
            UseCoordinate(stage, type == SamplerTextureType.TwoD ? 0x3 : 0x7);
            Emit(Opcode.Tex,
                DestinationToken(RegisterType.Temp, FirstTextureStandIn + stage, 0xF),
                Source(RegisterType.Texture, stage, Identity),
                Source(RegisterType.Sampler, stage, Identity));
        }

        private void LowerTexCoord(D3D9Instruction instruction)
        {
            int stage = instruction.GetParamRegisterNumber(0);
            UseCoordinate(stage, 0x7);
            int standIn = FirstTextureStandIn + stage;
            Emit(Opcode.Mov,
                DestinationToken(RegisterType.Temp, standIn, 0x7, ResultModifier.Saturate),
                Source(RegisterType.Texture, stage, Identity));
            Emit(Opcode.Mov,
                DestinationToken(RegisterType.Temp, standIn, 0x8),
                Helper(ReplicateY));
        }

        private void LowerTexld(D3D9Instruction instruction)
        {
            int stage = instruction.GetParamRegisterNumber(0);
            SamplerTextureType type = SamplerType(stage);
            _samplers[stage] = type;
            if (type != SamplerTextureType.TwoD && IsProjected(instruction, 1))
            {
                throw new NotImplementedException(
                    "a projected texld of a cube or volume texture, which ps_1_4 does not define");
            }
            uint coordinate = Coordinate(instruction, 1, type == SamplerTextureType.TwoD ? 0x3 : 0x7);
            Emit(Opcode.Tex,
                DestinationToken(RegisterType.Temp, stage, 0xF),
                coordinate,
                Source(RegisterType.Sampler, stage, Identity));
        }

        private void LowerTexcrd(D3D9Instruction instruction)
        {
            int mask = instruction.GetDestinationWriteMask();
            if ((mask & 0x8) != 0)
            {
                throw new NotImplementedException(
                    "a texcrd into an alpha, which ps_1_4 does not define");
            }
            if ((mask & 0x4) != 0 && IsProjected(instruction, 1))
            {
                throw new NotImplementedException(
                    "a projected texcrd into a blue, which ps_1_4 does not define");
            }
            uint coordinate = Coordinate(instruction, 1, mask);
            Emit(Opcode.Mov,
                DestinationToken(RegisterType.Temp, instruction.GetParamRegisterNumber(0), mask),
                coordinate);
        }

        private void LowerTexDepth(D3D9Instruction instruction)
        {
            EmitDepth(instruction.GetParamRegisterNumber(0));
        }

        // Depth = x / y, or 1 where y is 0.
        private void EmitDepth(int ratio)
        {
            int depth = TakeScratch();
            Emit(Opcode.Rcp, DestinationToken(RegisterType.Temp, depth, 0x1),
                Source(RegisterType.Temp, ratio, ReplicateY));
            Emit(Opcode.Mul, DestinationToken(RegisterType.Temp, depth, 0x2),
                Source(RegisterType.Temp, ratio, ReplicateX),
                Source(RegisterType.Temp, depth, ReplicateX));
            Emit(Opcode.Abs, DestinationToken(RegisterType.Temp, depth, 0x4),
                Source(RegisterType.Temp, ratio, ReplicateY));
            Emit(Opcode.Cmp, DestinationToken(RegisterType.Temp, depth, 0x8),
                Negate(Source(RegisterType.Temp, depth, ReplicateZ)),
                Helper(ReplicateY),
                Source(RegisterType.Temp, depth, ReplicateY));
            Emit(Opcode.Mov, DestinationToken(RegisterType.DepthOut, 0, 0xF),
                Source(RegisterType.Temp, depth, ReplicateW));
        }

        // Texture n's stand-in as an addressing instruction reads it, modifier
        // included (e.g. _bx2 on a normal map).
        private uint TextureColour(D3D9Instruction instruction, int index, int read) =>
            LowerSource(instruction, index, read);

        private uint CoordinateSet(int stage, int mask)
        {
            UseCoordinate(stage, mask);
            return Source(RegisterType.Texture, stage, Identity);
        }

        // Stage m sampled at a coordinate in a temp, into t(m)'s stand-in.
        private void Sample(int stage, uint coordinate)
        {
            _samplers[stage] = SamplerType(stage);
            Emit(Opcode.Tex,
                DestinationToken(RegisterType.Temp, FirstTextureStandIn + stage, 0xF),
                coordinate,
                Source(RegisterType.Sampler, stage, Identity));
        }

        private void LowerTexReg2(D3D9Instruction instruction, byte swizzle)
        {
            int stage = instruction.GetParamRegisterNumber(0);
            if (swizzle == Identity)
            {
                Sample(stage, TextureColour(instruction, 1, 0x7));
                return;
            }
            int read = (1 << (swizzle & 0x3)) | (1 << ((swizzle >> 2) & 0x3));
            int coordinate = TakeScratch();
            Emit(Opcode.Mov, DestinationToken(RegisterType.Temp, coordinate, 0x3),
                WithSwizzle(TextureColour(instruction, 1, read), swizzle));
            Sample(stage, Source(RegisterType.Temp, coordinate, Identity));
        }

        /// <summary>
        /// One texm3x2/texm3x3 row: coordinate m dotted with texture n, into the first
        /// pad's stand-in. Rows must be on consecutive stages; returns the stand-in.
        /// </summary>
        private int MatrixRow(D3D9Instruction instruction, bool finalRow, int rows)
        {
            int stage = instruction.GetParamRegisterNumber(0);
            if (_matrixRows == 0)
            {
                _matrixBase = stage;
            }
            if (stage != _matrixBase + _matrixRows || _matrixRows >= rows
                || finalRow != (_matrixRows == rows - 1))
            {
                throw new NotImplementedException(
                    $"{instruction.Opcode} out of the order of a {rows}-row texture matrix");
            }
            int vector = FirstTextureStandIn + _matrixBase;
            Emit(Opcode.Dp3, DestinationToken(RegisterType.Temp, vector, 1 << _matrixRows),
                CoordinateSet(stage, 0x7), TextureColour(instruction, 1, 0x7));
            _matrixRows++;
            if (finalRow)
            {
                _matrixRows = 0;
                _matrixBase = -1;
            }
            return vector;
        }

        /// <summary>
        /// Samples at 2N(N.E)/(N.N) - E. E is a constant for texm3x3spec, the three
        /// coordinates' w for texm3x3vspec.
        /// </summary>
        private void LowerTexM3x3Spec(D3D9Instruction instruction)
        {
            int stage = instruction.GetParamRegisterNumber(0);
            int normal = MatrixRow(instruction, finalRow: true, 3);
            uint eye;
            if (instruction.Opcode == Opcode.TexM3x3Spec)
            {
                eye = instruction.Params[2];
            }
            else
            {
                int eyeRegister = TakeScratch();
                for (int row = 0; row < 3; row++)
                {
                    Emit(Opcode.Mov, DestinationToken(RegisterType.Temp, eyeRegister, 1 << row),
                        WithSwizzle(CoordinateSet(stage - 2 + row, 0x8), ReplicateW));
                }
                eye = Source(RegisterType.Temp, eyeRegister, Identity);
            }
            uint n = Source(RegisterType.Temp, normal, Identity);
            int scale = TakeScratch();
            Emit(Opcode.Dp3, DestinationToken(RegisterType.Temp, scale, 0x1), n, eye);
            Emit(Opcode.Dp3, DestinationToken(RegisterType.Temp, scale, 0x2), n, n);
            Emit(Opcode.Rcp, DestinationToken(RegisterType.Temp, scale, 0x4),
                Source(RegisterType.Temp, scale, ReplicateY));
            Emit(Opcode.Mul, DestinationToken(RegisterType.Temp, scale, 0x8),
                Source(RegisterType.Temp, scale, ReplicateX),
                Source(RegisterType.Temp, scale, ReplicateZ));
            Emit(Opcode.Add, DestinationToken(RegisterType.Temp, scale, 0x8),
                Source(RegisterType.Temp, scale, ReplicateW),
                Source(RegisterType.Temp, scale, ReplicateW));
            int reflected = TakeScratch();
            Emit(Opcode.Mad, DestinationToken(RegisterType.Temp, reflected, 0x7),
                n, Source(RegisterType.Temp, scale, ReplicateW), Negate(eye));
            Sample(stage, Source(RegisterType.Temp, reflected, Identity));
        }

        /// <summary>
        /// mul(float2(r, g), bumpEnvMat) into a scratch .xy, as fxc compiles it (a
        /// dp2add per column) so it decompiles back to the mul.
        /// </summary>
        private int BumpOffset(int stage, uint value)
        {
            _bumpMatrices.Add(stage);
            _usesZero = true;
            int offset = TakeScratch();
            for (int column = 0; column < 2; column++)
            {
                Emit(Opcode.DP2Add, DestinationToken(RegisterType.Temp, offset, 1 << column),
                    value,
                    Source(RegisterType.Const, BumpMatrixBase + 2 * stage + column, Identity),
                    Source(RegisterType.Const, ZeroConstant, ReplicateX));
            }
            return offset;
        }

        private void LowerTexBem(D3D9Instruction instruction)
        {
            int stage = instruction.GetParamRegisterNumber(0);
            uint perturbation = TextureColour(instruction, 1,
                instruction.Opcode == Opcode.TexBeml ? 0x7 : 0x3);
            int coordinate = BumpOffset(stage, perturbation);
            Emit(Opcode.Add, DestinationToken(RegisterType.Temp, coordinate, 0x3),
                Source(RegisterType.Temp, coordinate, Identity), CoordinateSet(stage, 0x3));
            Sample(stage, Source(RegisterType.Temp, coordinate, Identity));
            if (instruction.Opcode != Opcode.TexBeml)
            {
                return;
            }

            // texbeml: scale by a luminance from texture n's blue.
            _bumpLuminances.Add(stage);
            int luminance = TakeScratch();
            Emit(Opcode.Mad, DestinationToken(RegisterType.Temp, luminance, 0x1, ResultModifier.Saturate),
                SelectComponent(perturbation, 2),
                Source(RegisterType.Const, BumpLuminanceBase + stage, ReplicateX),
                Source(RegisterType.Const, BumpLuminanceBase + stage, ReplicateY));
            int standIn = FirstTextureStandIn + stage;
            Emit(Opcode.Mul, DestinationToken(RegisterType.Temp, standIn, 0x7),
                Source(RegisterType.Temp, standIn, Identity),
                Source(RegisterType.Temp, luminance, ReplicateX));
        }

        // bem dst.rg, src0, src1: src0 + bump offset of src1, by stage dst's matrix.
        private void LowerBem(D3D9Instruction instruction)
        {
            if (instruction.GetDestinationResultShift() != 0)
            {
                throw new NotImplementedException("a scaled bem");
            }
            uint value = LowerSource(instruction, 1, 0x3);
            uint perturbation = LowerSource(instruction, 2, 0x3);
            int offset = BumpOffset(instruction.GetParamRegisterNumber(0), perturbation);
            Emit(Opcode.Add, Destination(instruction), value,
                Source(RegisterType.Temp, offset, Identity));
        }

        private static bool IsProjected(D3D9Instruction instruction, int index) =>
            instruction.GetSourceModifier(index) is SourceModifier.DivideByZ or SourceModifier.DivideByW;

        /// <summary>
        /// A ps_1_4 texld/texcrd coordinate as a register read whole: as is, or
        /// swizzled/projected into a scratch temp, computing only <paramref name="used"/>.
        /// </summary>
        private uint Coordinate(D3D9Instruction instruction, int index, int used)
        {
            uint plain = WithModifier(instruction.Params[index], SourceModifier.None);
            byte[] swizzle = instruction.GetSourceSwizzleComponents(index);
            int divisor = instruction.GetSourceModifier(index) switch
            {
                SourceModifier.None => -1,
                SourceModifier.DivideByZ => swizzle[2],
                SourceModifier.DivideByW => swizzle[3],
                SourceModifier modifier => throw new NotImplementedException(
                    $"source modifier {modifier} on a texture coordinate"),
            };

            if (instruction.GetParamRegisterType(index) == RegisterType.Texture)
            {
                int read = divisor < 0 ? 0 : 1 << divisor;
                for (int i = 0; i < 4; i++)
                {
                    if ((used & (1 << i)) != 0)
                    {
                        read |= 1 << swizzle[i];
                    }
                }
                UseCoordinate(instruction.GetParamRegisterNumber(index), read);
            }

            if (divisor < 0)
            {
                if (instruction.GetSourceSwizzle(index) == Identity)
                {
                    return plain;
                }
                int swizzled = TakeScratch();
                Emit(Opcode.Mov, DestinationToken(RegisterType.Temp, swizzled, used), plain);
                return Source(RegisterType.Temp, swizzled, Identity);
            }

            // The reciprocal goes in w, which nothing reads (texcrd to alpha is refused).
            int projected = TakeScratch();
            Emit(Opcode.Rcp, DestinationToken(RegisterType.Temp, projected, 0x8),
                WithSwizzle(plain, Replicate(divisor)));
            Emit(Opcode.Mul, DestinationToken(RegisterType.Temp, projected, used),
                plain, Source(RegisterType.Temp, projected, ReplicateW));
            return Source(RegisterType.Temp, projected, Identity);
        }

        private void LowerArithmetic(D3D9Instruction instruction, int? holdIn)
        {
            Opcode opcode = instruction.Opcode;

            // Modifiers are computed only for the components read, or unread lanes
            // stay alive in the decompilation.
            int read = opcode switch
            {
                Opcode.Dp3 => 0x7,
                Opcode.Dp4 => 0xF,
                _ => instruction.GetDestinationWriteMask(),
            };
            int sourceCount = instruction.Params.Count - 1;
            var sources = new uint[sourceCount];
            for (int i = 0; i < sourceCount; i++)
            {
                sources[i] = LowerSource(instruction, i + 1, read);
            }

            if (opcode == Opcode.Sub)
            {
                opcode = Opcode.Add;
                sources[1] = Negate(sources[1]);
            }

            if (opcode == Opcode.Cnd)
            {
                // cnd: a > 0.5 ? b : c; cmp: x >= 0 ? c : b, so x = 0.5 - a.
                int condition = TakeScratch();
                Emit(Opcode.Add, DestinationToken(RegisterType.Temp, condition, read),
                    Negate(sources[0]), Helper(ReplicateX));
                opcode = Opcode.Cmp;
                sources = [Source(RegisterType.Temp, condition, Identity), sources[2], sources[1]];
            }

            uint destination = Destination(instruction);
            if (holdIn != null)
            {
                destination = Retarget(destination, RegisterType.Temp, holdIn.Value);
            }

            int shift = instruction.GetDestinationResultShift();
            if (shift == 0)
            {
                Emit(opcode, [destination, .. sources]);
                return;
            }

            // Scale before saturate: the mul that scales carries the saturate.
            int unscaled = TakeScratch();
            Emit(opcode, [Retarget(ClearResultModifiers(destination), RegisterType.Temp, unscaled),
                .. sources]);
            Emit(Opcode.Mul, destination, Source(RegisterType.Temp, unscaled, Identity), Scale(shift));
        }

        /// <summary>
        /// A source ps_2_0 can read: t# as its stand-in, and a modifier ps_2_0 lacks
        /// applied into a scratch temp (only <paramref name="read"/> computed).
        /// </summary>
        private uint LowerSource(D3D9Instruction instruction, int index, int read)
        {
            uint token = instruction.Params[index];
            RegisterType type = instruction.GetParamRegisterType(index);
            if (type == RegisterType.Texture)
            {
                if (IsVersion14)
                {
                    UseCoordinate(instruction.GetParamRegisterNumber(index), 0xF);
                }
                else
                {
                    token = Retarget(token, RegisterType.Temp,
                        FirstTextureStandIn + instruction.GetParamRegisterNumber(index));
                }
            }

            SourceModifier modifier = instruction.GetSourceModifier(index);
            if (modifier is SourceModifier.None or SourceModifier.Negate)
            {
                return token;
            }

            uint plain = WithModifier(token, SourceModifier.None);
            int scratch = TakeScratch();
            uint result = DestinationToken(RegisterType.Temp, scratch, read);
            switch (modifier)
            {
                case SourceModifier.Bias:
                    Emit(Opcode.Add, result, plain, Negate(Helper(ReplicateX)));
                    break;
                case SourceModifier.BiasAndNegate:
                    Emit(Opcode.Add, result, Negate(plain), Helper(ReplicateX));
                    break;
                case SourceModifier.Sign:
                    Emit(Opcode.Mad, result, plain, Helper(ReplicateZ), Helper(ReplicateW));
                    break;
                case SourceModifier.SignAndNegate:
                    Emit(Opcode.Mad, result, plain, Negate(Helper(ReplicateZ)), Helper(ReplicateY));
                    break;
                case SourceModifier.Complement:
                    Emit(Opcode.Add, result, Negate(plain), Helper(ReplicateY));
                    break;
                case SourceModifier.X2:
                    Emit(Opcode.Mul, result, plain, Helper(ReplicateZ));
                    break;
                case SourceModifier.X2AndNegate:
                    Emit(Opcode.Mul, result, plain, Negate(Helper(ReplicateZ)));
                    break;
                default:
                    throw new NotImplementedException($"source modifier {modifier} in a ps_1_x pixel shader");
            }
            return Source(RegisterType.Temp, scratch, Identity);
        }

        private uint Destination(D3D9Instruction instruction)
        {
            uint token = instruction.Params[0] & ~0x0F000000u; // the shift is lowered apart
            return !IsVersion14 && instruction.GetParamRegisterType(0) == RegisterType.Texture
                ? Retarget(token, RegisterType.Temp,
                    FirstTextureStandIn + instruction.GetParamRegisterNumber(0))
                : token;
        }

        private static bool ReadsRegisterWrittenBy(D3D9Instruction reader, D3D9Instruction writer)
        {
            if (!HasArithmeticDestination(writer))
            {
                return false;
            }
            RegisterKey written = writer.GetParamRegisterKey(0);
            for (int i = 1; i < reader.Params.Count; i++)
            {
                if (reader.GetParamRegisterKey(i).Equals(written))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasArithmeticDestination(D3D9Instruction instruction) =>
            instruction.Opcode is Opcode.Add or Opcode.Sub or Opcode.Mul or Opcode.Mad
                or Opcode.Lrp or Opcode.Mov or Opcode.Dp3 or Opcode.Dp4
                or Opcode.Cmp or Opcode.Cnd;

        private SamplerTextureType SamplerType(int stage)
        {
            foreach (D3D9Instruction comment in shader.Instructions.OfType<D3D9Instruction>()
                .Where(i => i.Opcode == Opcode.Comment))
            {
                using var reader = new ConstantTableCommentReader(comment);
                ConstantTable table = reader.ReadTable();
                D3D9ConstantDeclaration sampler = table?.Declarations.FirstOrDefault(d =>
                    d.RegisterSet == RegisterSet.Sampler && d.ContainsIndex(stage));
                if (sampler != null)
                {
                    return sampler.TypeInfo.ParameterType switch
                    {
                        ParameterType.SamplerCube => SamplerTextureType.Cube,
                        ParameterType.Sampler3D => SamplerTextureType.Volume,
                        _ => SamplerTextureType.TwoD,
                    };
                }
            }
            return SamplerTextureType.TwoD;
        }

        private void UseCoordinate(int stage, int mask)
        {
            _coordinateMasks[stage] = _coordinateMasks.GetValueOrDefault(stage) | mask;
        }

        private int TakeScratch()
        {
            if (_nextScratch > LastScratch)
            {
                throw new InvalidOperationException("ran out of scratch registers");
            }
            return _nextScratch++;
        }

        private uint Helper(byte swizzle)
        {
            _usesHelper = true;
            return Source(RegisterType.Const, HelperConstant, swizzle);
        }

        private uint Scale(int shift)
        {
            switch (shift)
            {
                case 1:
                    return Helper(ReplicateZ);
                case -1:
                    return Helper(ReplicateX);
            }
            _usesScale = true;
            return Source(RegisterType.Const, ScaleConstant, shift switch
            {
                2 => ReplicateX,
                3 => ReplicateY,
                -2 => ReplicateZ,
                -3 => ReplicateW,
                _ => throw new NotImplementedException($"a result scale of 2^{shift}"),
            });
        }

        private void Emit(Opcode opcode, params uint[] parameters)
        {
            foreach (int colour in parameters.Skip(1)
                .Where(p => RegisterTypeOf(p) == RegisterType.Input)
                .Select(p => (int)(p & 0x7FF)))
            {
                _colours.Add(colour);
            }
            _body.Add([(uint)opcode | ((uint)parameters.Length << 24), .. parameters]);
        }

        private ShaderModel Assemble()
        {
            var words = new List<uint> { 0xFFFF0200 };
            foreach (uint[] comment in _comments)
            {
                words.AddRange(comment);
            }
            if (_bumpMatrices.Count != 0 || _bumpLuminances.Count != 0)
            {
                words.AddRange(StageStateTable());
            }

            foreach ((int stage, int mask) in _coordinateMasks)
            {
                AddInstruction(words, Opcode.Dcl, 0x80000000,
                    DestinationToken(RegisterType.Texture, stage, mask));
            }
            foreach (int colour in _colours)
            {
                AddInstruction(words, Opcode.Dcl, 0x80000000,
                    DestinationToken(RegisterType.Input, colour, 0xF));
            }
            foreach ((int stage, SamplerTextureType type) in _samplers)
            {
                AddInstruction(words, Opcode.Dcl, 0x80000000 | ((uint)type << 27),
                    DestinationToken(RegisterType.Sampler, stage, 0xF));
            }

            foreach (uint[] definition in _definitions)
            {
                AddInstruction(words, Opcode.Def, definition);
            }
            if (_usesHelper)
            {
                AddDefinition(words, HelperConstant, 0.5f, 1, 2, -1);
            }
            if (_usesScale)
            {
                AddDefinition(words, ScaleConstant, 4, 8, 0.25f, 0.125f);
            }
            if (_usesZero)
            {
                AddDefinition(words, ZeroConstant, 0, 0, 0, 0);
            }

            foreach (uint[] instruction in _body)
            {
                words.AddRange(instruction);
            }
            words.Add(0x0000FFFF);

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            {
                foreach (uint word in words)
                {
                    writer.Write(word);
                }
            }
            stream.Position = 0;
            using var shaderReader = new ShaderReader(stream, true);
            return shaderReader.ReadShader();
        }

        /// <summary>
        /// A separate CTAB comment declaring the bump stage state as uniforms, in
        /// D3DXSHADER_CONSTANTTABLE layout (offsets from the table start).
        /// </summary>
        private uint[] StageStateTable()
        {
            var constants = new List<(string Name, int Register, int Rows, ParameterClass Class)>();
            constants.AddRange(_bumpMatrices.Select(s =>
                ($"bumpEnvMat{s}", BumpMatrixBase + 2 * s, 2, ParameterClass.MatrixColumns)));
            constants.AddRange(_bumpLuminances.Select(s =>
                ($"bumpEnvLum{s}", BumpLuminanceBase + s, 1, ParameterClass.Vector)));

            const int HeaderSize = 0x1C;
            const int ConstantInfoSize = 20;
            const int TypeInfoSize = 16;
            int typesStart = HeaderSize + constants.Count * ConstantInfoSize;
            int stringsStart = typesStart + constants.Count * TypeInfoSize;
            var strings = new List<byte>();
            int AddString(string value)
            {
                int offset = stringsStart + strings.Count;
                strings.AddRange(System.Text.Encoding.ASCII.GetBytes(value));
                strings.Add(0);
                return offset;
            }

            using var table = new MemoryStream();
            using (var writer = new BinaryWriter(table, System.Text.Encoding.ASCII, leaveOpen: true))
            {
                var nameOffsets = constants.Select(c => AddString(c.Name)).ToList();
                int creator = AddString("HlslDecompiler");
                int target = AddString("ps_2_0");

                writer.Write(HeaderSize);
                writer.Write(creator);
                writer.Write(0xFFFF0200u);
                writer.Write(constants.Count);
                writer.Write(HeaderSize);
                writer.Write(0); // flags
                writer.Write(target);
                for (int i = 0; i < constants.Count; i++)
                {
                    writer.Write(nameOffsets[i]);
                    writer.Write((short)RegisterSet.Float4);
                    writer.Write((short)constants[i].Register);
                    writer.Write((short)constants[i].Rows);
                    writer.Write((short)0);
                    writer.Write(typesStart + i * TypeInfoSize);
                    writer.Write(0); // no default
                }
                foreach ((_, _, int rows, ParameterClass parameterClass) in constants)
                {
                    writer.Write((short)parameterClass);
                    writer.Write((short)ParameterType.Float);
                    writer.Write((short)rows);
                    writer.Write((short)2);
                    writer.Write((short)1);
                    writer.Write((short)0);
                    writer.Write(0);
                }
                writer.Write(strings.ToArray());
                while (table.Length % 4 != 0)
                {
                    writer.Write((byte)0);
                }
            }

            byte[] bytes = table.ToArray();
            var words = new List<uint>
            {
                (uint)Opcode.Comment | ((uint)(bytes.Length / 4 + 1) << 16),
                (uint)FourCC.Make("CTAB"),
            };
            for (int i = 0; i < bytes.Length; i += 4)
            {
                words.Add(BitConverter.ToUInt32(bytes, i));
            }
            return [.. words];
        }

        private static void AddInstruction(List<uint> words, Opcode opcode, params uint[] parameters)
        {
            words.Add((uint)opcode | ((uint)parameters.Length << 24));
            words.AddRange(parameters);
        }

        private static void AddDefinition(List<uint> words, int register, float x, float y, float z, float w)
        {
            AddInstruction(words, Opcode.Def,
                DestinationToken(RegisterType.Const, register, 0xF),
                BitConverter.SingleToUInt32Bits(x), BitConverter.SingleToUInt32Bits(y),
                BitConverter.SingleToUInt32Bits(z), BitConverter.SingleToUInt32Bits(w));
        }

        private static uint[] ParamTokens(D3D9Instruction instruction) =>
            [.. Enumerable.Range(0, instruction.Params.Count).Select(i => instruction.Params[i])];
    }

    private static uint RegisterBits(RegisterType type, int number) =>
        (((uint)type & 0x7) << 28) | (((uint)type & 0x18) << 8) | (uint)number;

    private static RegisterType RegisterTypeOf(uint token) =>
        (RegisterType)(((token >> 28) & 0x7) | ((token >> 8) & 0x18));

    private static uint DestinationToken(RegisterType type, int number, int mask,
        ResultModifier modifier = ResultModifier.None) =>
        0x80000000 | RegisterBits(type, number) | ((uint)mask << 16) | ((uint)modifier << 20);

    private static uint Source(RegisterType type, int number, byte swizzle) =>
        0x80000000 | RegisterBits(type, number) | ((uint)swizzle << 16);

    private static uint Retarget(uint token, RegisterType type, int number) =>
        (token & ~0x70001FFFu) | RegisterBits(type, number);

    private static uint ClearResultModifiers(uint destination) => destination & ~0x00F00000u;

    private static uint SelectComponent(uint source, int component) =>
        WithSwizzle(source, Replicate((int)((source >> (16 + 2 * component)) & 0x3)));

    private static uint WithSwizzle(uint source, byte swizzle) =>
        (source & ~0x00FF0000u) | ((uint)swizzle << 16);

    private static byte Replicate(int component) =>
        (byte)(component | component << 2 | component << 4 | component << 6);

    private static uint WithModifier(uint source, SourceModifier modifier) =>
        (source & ~0x0F000000u) | ((uint)modifier << 24);

    private static uint Negate(uint source) =>
        (SourceModifier)((source >> 24) & 0xF) switch
        {
            SourceModifier.None => WithModifier(source, SourceModifier.Negate),
            SourceModifier.Negate => WithModifier(source, SourceModifier.None),
            _ => throw new InvalidOperationException("negating a modified source"),
        };
}
