using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// D3D9 instructions fxc never emits from HLSL but that hand-assembled shaders can
// hold. Expected text is `fxc /dumpbin` output, except this writer spells out
// swizzles fxc elides.
public class AssemblyOnlyInstructionTests
{
    [Test]
    public void DisassemblesTheInstructionsNoHlslCompilesInto()
    {
        ShaderModel shader = Assemble(ShaderType.Vertex, 3, [
            Declaration(DeclUsage.Position, RegisterType.Input, 0),
            Declaration(DeclUsage.Position, RegisterType.Output, 0),
            Constant(0, 1, 2, 3, 4),
            Constant(1, 5, 6, 7, 8),
            Constant(2, 9, 10, 11, 12),
            Constant(3, 13, 14, 15, 16),
            Instruction(Opcode.M4x4, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Input, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.M4x3, Destination(RegisterType.Temp, 1, 0x7),
                Source(RegisterType.Input, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.M3x4, Destination(RegisterType.Temp, 2),
                Source(RegisterType.Input, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.M3x3, Destination(RegisterType.Temp, 3, 0x7),
                Source(RegisterType.Input, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.M3x2, Destination(RegisterType.Temp, 4, 0x3),
                Source(RegisterType.Input, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.Crs, Destination(RegisterType.Temp, 5, 0x7),
                Source(RegisterType.Temp, 0, SourceModifier.Negate), Source(RegisterType.Temp, 2)),
            // sgn takes two scratch registers after the value.
            Instruction(Opcode.Sgn, Destination(RegisterType.Temp, 6, 0x3),
                Source(RegisterType.Temp, 1), Source(RegisterType.Temp, 3), Source(RegisterType.Temp, 4)),
            Instruction(Opcode.ExpP, Destination(RegisterType.Temp, 7, 0x1),
                Source(RegisterType.Temp, 5, SourceModifier.Abs, 0x00)),
            Instruction(Opcode.LogP, Destination(RegisterType.Temp, 7, 0x2),
                Source(RegisterType.Temp, 6, swizzle: 0x55)),
            Instruction(Opcode.Cnd, Destination(RegisterType.Output, 0, modifier: ResultModifier.Saturate),
                Source(RegisterType.Temp, 7), Source(RegisterType.Const, 1), Source(RegisterType.Const, 2)),
            Instruction(Opcode.End),
        ]);

        Assert.That(WriteAsm(shader), Is.EqualTo("""
            vs_3_0
            dcl_position v0
            dcl_position o0
            def c0, 1, 2, 3, 4
            def c1, 5, 6, 7, 8
            def c2, 9, 10, 11, 12
            def c3, 13, 14, 15, 16
            m4x4 r0, v0, c0
            m4x3 r1.xyz, v0, c0
            m3x4 r2, v0.xyz, c0.xyz
            m3x3 r3.xyz, v0.xyz, c0.xyz
            m3x2 r4.xy, v0.xyz, c0.xyz
            crs r5.xyz, -r0.xyz, r2.xyz
            sgn r6.xy, r1.xy, r3.xy, r4.xy
            expp r7.x, r5_abs.x
            logp r7.y, r6.y
            cnd_sat o0, r7, c1, c2

            """));
    }

    // d3dcompiler_47 rejects ps_1_x (X3539). SM1 tokens carry no length, so the
    // reader must know each opcode's operand count.
    [Test]
    public void DisassemblesTheTextureAddressingSetOfShaderModel1()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 1, 3, [
            Constant(0, 1, 2, 3, 4),
            FixedSizeInstruction(Opcode.Tex, Destination(RegisterType.Texture, 0)),
            FixedSizeInstruction(Opcode.TexCoord, Destination(RegisterType.Texture, 1)),
            Pair(Opcode.TexBem, 2), Pair(Opcode.TexBeml, 3),
            Pair(Opcode.TexReg2AR, 2), Pair(Opcode.TexReg2GB, 2), Pair(Opcode.TexReg2RGB, 2),
            Pair(Opcode.TeXM3x2Pad, 1), Pair(Opcode.TexM3x2Tex, 2), Pair(Opcode.TexM3x2Depth, 2),
            Pair(Opcode.TeXM3x3Pad, 1), Pair(Opcode.TexM3x3Tex, 3), Pair(Opcode.TexM3x3, 3),
            FixedSizeInstruction(Opcode.TexM3x3Spec, Destination(RegisterType.Texture, 3),
                Source(RegisterType.Texture, 0), Source(RegisterType.Const, 0)),
            Pair(Opcode.TexM3x3VSpec, 3),
            Pair(Opcode.TexDP3, 2), Pair(Opcode.TexDP3Tex, 2),
            FixedSizeInstruction(Opcode.TexKill, Destination(RegisterType.Texture, 0)),
            FixedSizeInstruction(Opcode.Cnd, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Temp, 0), Source(RegisterType.Texture, 0),
                Source(RegisterType.Texture, 1)),
            FixedSizeInstruction(Opcode.Lrp, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Temp, 0), Source(RegisterType.Texture, 0),
                Source(RegisterType.Texture, 1)),
            FixedSizeInstruction(Opcode.Mov, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Texture, 3)),
            FixedSizeInstruction(Opcode.End),
        ]);

        Assert.That(WriteAsm(shader), Is.EqualTo("""
            ps_1_3
            def c0, 1, 2, 3, 4
            tex t0
            texcoord t1
            texbem t2, t0
            texbeml t3, t0
            texreg2ar t2, t0
            texreg2gb t2, t0
            texreg2rgb t2, t0
            texm3x2pad t1, t0
            texm3x2tex t2, t0
            texm3x2depth t2, t0
            texm3x3pad t1, t0
            texm3x3tex t3, t0
            texm3x3 t3, t0
            texm3x3spec t3, t0, c0
            texm3x3vspec t3, t0
            texdp3 t2, t0
            texdp3tex t2, t0
            texkill t0
            cnd r0, r0, t0, t1
            lrp r0, r0, t0, t1
            mov r0, t3

            """));
    }

    // _bias, _bx2, 1-x, result scales _x8.._d8 (written before _sat), co-issue +.
    [Test]
    public void DisassemblesTheModifiersOfShaderModel1()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 1, 1, [
            Constant(0, 1, 0.5f, 0.25f, 0),
            FixedSizeInstruction(Opcode.Tex, Destination(RegisterType.Texture, 0)),
            FixedSizeInstruction(Opcode.TexCoord, Destination(RegisterType.Texture, 1)),
            FixedSizeInstruction(Opcode.Add, Rd(0, 0x7, ResultModifier.Saturate) | Scale(1),
                Source(RegisterType.Texture, 0, SourceModifier.Sign),
                V(0, modifier: SourceModifier.Complement)),
            CoIssued(FixedSizeInstruction(Opcode.Mov, Rd(0, 0x8), C(0, 0xFF, SourceModifier.Bias))),
            FixedSizeInstruction(Opcode.Mul, Rd(1) | Scale(-1), R(0),
                Source(RegisterType.Texture, 1, SourceModifier.BiasAndNegate)),
            FixedSizeInstruction(Opcode.Mul, Rd(1) | Scale(2), R(1),
                Source(RegisterType.Texture, 1, SourceModifier.SignAndNegate)),
            FixedSizeInstruction(Opcode.Mul, Rd(1, modifier: ResultModifier.Saturate) | Scale(3), R(1),
                Source(RegisterType.Texture, 1, swizzle: 0xAA)),
            FixedSizeInstruction(Opcode.Mul, Rd(1) | Scale(-2), R(1), V(1)),
            FixedSizeInstruction(Opcode.Mul, Rd(1) | Scale(-3), R(1), V(1)),
            FixedSizeInstruction(Opcode.Cnd, Rd(0), R(0, 0xFF),
                Source(RegisterType.Texture, 0), R(1)),
            FixedSizeInstruction(Opcode.End),
        ]);

        Assert.That(WriteAsm(shader), Is.EqualTo("""
            ps_1_1
            def c0, 1, 0.5, 0.25, 0
            tex t0
            texcoord t1
            add_x2_sat r0.xyz, t0_bx2.xyz, 1-v0.xyz
            + mov r0.w, c0_bias.w
            mul_d2 r1, r0, -t1_bias
            mul_x4 r1, r1, -t1_bx2
            mul_x8_sat r1, r1, t1.z
            mul_d4 r1, r1, v1
            mul_d8 r1, r1, v1
            cnd r0, r0.w, t0, r1

            """));
    }

    // Signed power-of-two shift in the four bits above the result modifier.
    private static uint Scale(int shift)
    {
        return (uint)(shift & 0xF) << 24;
    }

    private static uint[] CoIssued(uint[] instruction)
    {
        instruction[0] |= 0x40000000;
        return instruction;
    }

    // ps_1_4 texcrd/texld/phase. This texld has no sampler operand, unlike ps_2_0's.
    [Test]
    public void DisassemblesWhatShaderModel1_4Renamed()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 1, 4, [
            Constant(0, 1, 2, 3, 4),
            FixedSizeInstruction(Opcode.TexCoord, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Texture, 0)),
            FixedSizeInstruction(Opcode.Tex, Destination(RegisterType.Temp, 1),
                Source(RegisterType.Texture, 1)),
            FixedSizeInstruction(Opcode.Bem, Destination(RegisterType.Temp, 0, 0x3),
                Source(RegisterType.Temp, 0), Source(RegisterType.Temp, 1)),
            FixedSizeInstruction(Opcode.Phase),
            FixedSizeInstruction(Opcode.Tex, Destination(RegisterType.Temp, 2),
                Source(RegisterType.Temp, 0)),
            FixedSizeInstruction(Opcode.TexDepth, Destination(RegisterType.Temp, 5)),
            FixedSizeInstruction(Opcode.Mov, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Temp, 2)),
            FixedSizeInstruction(Opcode.End),
        ]);

        // fxc writes `bem r0.xy, r0, r1`.
        Assert.That(WriteAsm(shader), Is.EqualTo("""
            ps_1_4
            def c0, 1, 2, 3, 4
            texcrd r0, t0
            texld r1, t1
            bem r0.xy, r0.xy, r1.xy
            phase
            texld r2, r0
            texdepth r5
            mov r0, r2

            """));
    }

    private static uint[] Pair(Opcode opcode, int destination)
    {
        return FixedSizeInstruction(opcode, Destination(RegisterType.Texture, destination),
            Source(RegisterType.Texture, 0));
    }

    // fxc always inlines calls and lacks the _2_x profiles, so no golden reaches
    // these. D3DSIO_BREAKP prints as `break p0`; its predicate is a destination token.
    [Test]
    public void DisassemblesSubroutinesAndPredication()
    {
        ShaderModel shader = Assemble(ShaderType.Vertex, 3, [
            Declaration(DeclUsage.Position, RegisterType.Input, 0),
            Declaration(DeclUsage.Position, RegisterType.Output, 0),
            Constant(0, 2, 3, 5, 7),
            ConstantInt(0, 4, 0, 1, 0),
            ConstantBool(0, true),
            Instruction(Opcode.Mov, Destination(RegisterType.Temp, 0), Source(RegisterType.Input, 0)),
            Instruction(Opcode.Call, Source(RegisterType.Label, 1)),
            Instruction(Opcode.CallNZ, Source(RegisterType.Label, 2), Source(RegisterType.ConstBool, 0)),
            Instruction(Opcode.Rep, Source(RegisterType.ConstInt, 0)),
            Instruction(Opcode.SetP, IfComparison.GT, Destination(RegisterType.Predicate, 0),
                Source(RegisterType.Temp, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.Breakp, Destination(RegisterType.Predicate, 0)),
            Instruction(Opcode.Break),
            Instruction(Opcode.EndRep),
            Instruction(Opcode.Mov, Destination(RegisterType.Output, 0), Source(RegisterType.Temp, 0)),
            Instruction(Opcode.Ret),
            Instruction(Opcode.Label, Source(RegisterType.Label, 1)),
            Instruction(Opcode.Add, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Temp, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.Ret),
            Instruction(Opcode.Label, Source(RegisterType.Label, 2)),
            Instruction(Opcode.Mov, Destination(RegisterType.Temp, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.Ret),
            Instruction(Opcode.End),
        ]);

        Assert.That(WriteAsm(shader), Is.EqualTo("""
            vs_3_0
            dcl_position v0
            dcl_position o0
            def c0, 2, 3, 5, 7
            defi i0, 4, 0, 1, 0
            defb b0, true
            mov r0, v0
            call l1
            callnz l2, b0
            rep i0
            setp_gt p0, r0, c0
            break p0
            break
            endrep
            mov o0, r0
            ret
            label l1
            add r0, r0, c0
            ret
            label l2
            mov r0, c0
            ret

            """));
    }

    // m3x2 writes two components but reads three.
    [Test]
    public void AMatrixProductReadsTheWholeVectorHoweverFewRowsItWrites()
    {
        ShaderModel shader = Assemble(ShaderType.Vertex, 3, [
            Declaration(DeclUsage.Position, RegisterType.Input, 0),
            Declaration(DeclUsage.Position, RegisterType.Output, 0),
            Constant(0, 1, 2, 3, 4),
            Instruction(Opcode.M3x2, Destination(RegisterType.Output, 0, 0x3),
                Source(RegisterType.Input, 0), Source(RegisterType.Const, 0)),
            Instruction(Opcode.End),
        ]);

        Assert.That(WriteAsm(shader), Does.Contain("m3x2 o0.xy, v0.xyz, c0.xyz"));
    }

    // fxc's order: sign, register, modifier, then swizzle.
    [Test]
    public void ASourceModifierBindsToTheRegisterAndTheSwizzleFollowsIt()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0, 0x1),
                Source(RegisterType.Input, 0, SourceModifier.AbsAndNegate, 0xAA)),
            Instruction(Opcode.End),
        ]);

        Assert.That(WriteAsm(shader), Does.Contain("mov oC0.x, -v0_abs.z"));
    }
}
