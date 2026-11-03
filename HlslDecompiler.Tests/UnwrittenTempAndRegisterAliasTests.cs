using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// Two shapes no fxc compile holds but XDK microcode ships: a constant table
// naming one register twice, and a read of a temp before any write.
public class UnwrittenTempAndRegisterAliasTests
{
    // A corpus constant table names one float4 register twice; the second
    // name used to throw from DeclareRegister (an already-added key).
    [Test]
    public void DeclaresOneRegisterTwiceNamedKeepsTheFirstDeclaration()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0),
                Source(RegisterType.Const, 0)),
            Instruction(Opcode.End),
        ]);

        var state = new RegisterState(shader);
        var key = new D3D9RegisterKey(RegisterType.Const, 0);
        state.DeclareRegister(key, 0b1111);
        RegisterDeclaration first = state.RegisterDeclarations[key];

        Assert.That(() => state.DeclareRegister(key, 0b1111), Throws.Nothing);
        Assert.That(state.RegisterDeclarations[key], Is.SameAs(first));
    }

    // The AST parser used to throw KeyNotFoundException here; the read now
    // folds to zero, which is what an unwritten register reads.
    [Test]
    public void ReadsATempBeforeAnyWriteReadsZero()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            // r5 is never written.
            Instruction(Opcode.Mov, Destination(RegisterType.Temp, 0),
                Source(RegisterType.Temp, 5)),
            Instruction(Opcode.Mov, Destination(RegisterType.ColorOut, 0),
                Source(RegisterType.Temp, 0)),
            Instruction(Opcode.End),
        ]);

        string hlsl = WriteHlslAst(shader);

        Assert.Multiple(() =>
        {
            // Before the fix the parser threw on r5; a regression would
            // surface as an undeclared t5 or no output at all.
            Assert.That(hlsl, Does.Not.Contain("t5"));
            Assert.That(hlsl, Does.Contain("0"));
        });
    }
}
