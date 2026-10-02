using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// The arithmetic half of D3D9 assembly that fxc compiles nothing into: it expands
// a matrix product into dp4s, a cross() into mul and mad, sign() into comparisons,
// and has no syntax at all for the partial-precision exp and log or for cnd. Every
// one of them is still a legal instruction that a shader assembled by hand - or by
// an older compiler - can hold, and the reader and the interpreter already carry
// them, so the disassembler is the only writer they are missing. The expected text
// below is fxc's own, from `fxc /dumpbin` over the same assembled bytes, except
// where this writer names the components an operand reads instead of eliding a
// full-width swizzle the way fxc does.
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
            // sgn names two scratch registers after the value, which is what makes
            // it the only three-source instruction here.
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

    // The ps_1_x texture addressing set, which no profile fxc still knows can
    // reach: d3dcompiler_47 answers X3539 for a ps_1_x target. Shader model 1 is
    // also the one that carries no length field in the instruction token, so the
    // reader takes each operand count from the opcode - and until it knew these,
    // a ps_1_x pixel shader threw in the READER and no writer saw it at all. The
    // expected text is fxc's own, from /dumpbin over the same assembled bytes.
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

    // ps_1_4 renamed two instructions and gave each the register the older one
    // implied, and added a phase to split the shader in two. texld is NOT the
    // ps_2_0 one: it names no sampler, which is the operand a ps_2_0 texld reads
    // third and this one does not have.
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

        // fxc writes `bem r0.xy, r0, r1`, eliding a full width swizzle where this
        // writer names the two components the instruction reads.
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

    // Subroutines and predication. fxc inlines every function call at every D3D9
    // profile and there is no noinline to ask it not to, and it knows neither
    // ps_2_x nor vs_2_x, where predication belonged - so none of these reaches a
    // golden either. Two of them are not spelled the way the opcode is named:
    // D3DSIO_BREAKP disassembles as `break p0`, and its predicate is a destination
    // token, which is how fxc tells `break p0` from `break p0.z`.
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

    // m3x2 writes two components and reads three, so the destination mask is no
    // guide to how wide its operands are. Writing `m3x2 r0.xy, v0.xy, c0.xy` would
    // name a vector a third shorter than the one the instruction multiplies.
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

    // fxc writes -v0_abs.z: the sign in front of the whole operand, the modifier
    // against the register, and the swizzle selecting out of what the modifier
    // produced. The corpus holds this shape wherever a modified source carries a
    // swizzle - ps_3_0/negate_absolute is the same instruction.
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
