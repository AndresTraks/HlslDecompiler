using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A branch that updates a register it joins (`cmp r0, -r0, a, b`) names that
// write with the register's entry variable. Renaming the entry variable onto the
// join's variable moves the reads from above the if onto it too, where they
// precede its declaration (X3004); renaming none leaves the reads after the if on
// the entry value, which compiles and computes the wrong thing. So each case also
// runs both shaders.
public class SelfUpdatingBranchJoinTests
{
    private const uint X = 0x00;    // .x replicated
    private const uint Y = 0x55;    // .y replicated
    private const uint W = 0xFF;    // .w replicated

    // oC0, not a declared o0: the run of fxc's compile names its output COLOR0,
    // and CompareRuns compares only the names both sides write.
    private static uint Oc(uint mask = 0xF) => Destination(RegisterType.ColorOut, 0, mask);

    [Test]
    public void BranchUpdatingTheJoinedRegisterKeepsPreIfReadsOnTheirOwnVariable()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 2f, 3f),
            Constant(1, 4f, 5f, 6f, 7f),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 1, 1),
            Instruction(Opcode.Mul, Rd(0), V(0), C(1)),                       // entry value, read twice
            Instruction(Opcode.Mul, Rd(3), R(0), V(1)),                       // before the if
            Instruction(Opcode.Mul, Oc(0x8), R(0, W), C(0, Y)),            // and again
            Instruction(Opcode.IfC, IfComparison.LT, V(1, X, SourceModifier.Negate), C(0, X)),
            Instruction(Opcode.IfC, IfComparison.LT, V(1, Y, SourceModifier.Negate), C(0, X)),
            Instruction(Opcode.Mov, Rd(0), C(0)),                             // r0 = c0
            Instruction(Opcode.Else),
            Instruction(Opcode.Mov, Rd(0), C(1)),                             // r0 = c1
            Instruction(Opcode.Endif),
            Instruction(Opcode.Else),
            Instruction(Opcode.Cmp, Rd(0), R(0, X, SourceModifier.Negate), C(0), C(1)),
            Instruction(Opcode.Endif),                                        // r0 = -r0.x > 0 ? c0 : c1
            Instruction(Opcode.Mul, Oc(0x7), R(0), V(0)),                  // o0.xyz = r0 * i
            Instruction(Opcode.Mul, Rd(3), R(3), C(0, Y)),                    // r3 * 2, read after
            Instruction(Opcode.Mov, Oc(0x8), R(3, X)),                     // o0.w = r3.x * 2
            Instruction(Opcode.End),
        ]);

        AssertDecompiles(shader, """
            struct PS_IN
            {
            	float4 texcoord : TEXCOORD;
            	float4 texcoord1 : TEXCOORD1;
            };

            float4 main(PS_IN i) : COLOR
            {
            	float t0 = 4 * i.texcoord.x;
            	float t1 = t0 * i.texcoord1.x;
            	float3 t2;
            	if (i.texcoord1.x > 0) {
            		if (i.texcoord1.y > 0) {
            			t2 = float3(0, 1, 2);
            		} else {
            			t2 = float3(4, 5, 6);
            		}
            	} else {
            		t2 = -t0 >= 0 ? float3(0, 1, 2) : float3(4, 5, 6);
            	}
            	return float4(t2 * i.texcoord.xyz, t1);
            }

            """);
    }

    [Test]
    public void TrueBranchUpdatingTheJoinedRegister()
    {
        // The self-updating write comes first, so its variable - the entry's - is
        // the one the other branch is put on.
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 2f, 3f),
            Constant(1, 4f, 5f, 6f, 7f),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 1, 1),
            Instruction(Opcode.Mul, Rd(0), V(0), C(1)),
            Instruction(Opcode.Mul, Rd(3), R(0), V(1)),                       // read before the if
            Instruction(Opcode.IfC, IfComparison.LT, V(1, X, SourceModifier.Negate), C(0, X)),
            Instruction(Opcode.Cmp, Rd(0), R(0, X, SourceModifier.Negate), C(0), C(1)),
            Instruction(Opcode.Else),
            Instruction(Opcode.IfC, IfComparison.LT, V(1, Y, SourceModifier.Negate), C(0, X)),
            Instruction(Opcode.Mov, Rd(0), C(0)),
            Instruction(Opcode.Else),
            Instruction(Opcode.Mov, Rd(0), C(1)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.Endif),
            Instruction(Opcode.Mul, Oc(0x7), R(0), V(0)),                  // read after
            Instruction(Opcode.Mov, Oc(0x8), R(3, X)),
            Instruction(Opcode.End),
        ]);

        AssertDecompiles(shader, """
            struct PS_IN
            {
            	float4 texcoord : TEXCOORD;
            	float4 texcoord1 : TEXCOORD1;
            };

            float4 main(PS_IN i) : COLOR
            {
            	float t0 = 4 * i.texcoord.x;
            	float t1 = t0 * i.texcoord1.x;
            	float2 t2;
            	if (i.texcoord1.x > 0) {
            		t2 = -t0 >= 0 ? float2(1, 2) : float2(5, 6);
            		t0 = -t0 >= 0 ? 0 : 4;
            	} else {
            		if (i.texcoord1.y > 0) {
            			t0 = 0;
            			t2 = float2(1, 2);
            		} else {
            			t0 = 4;
            			t2 = float2(5, 6);
            		}
            	}
            	return float4(t0 * i.texcoord.x, t2 * i.texcoord.yz, t1);
            }

            """);
    }

    [Test]
    public void BranchUpdatingTheJoinedRegisterInALoop()
    {
        // The read above the if is in the loop body, so from the second iteration
        // on it follows the if: it must see what the previous iteration joined.
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 2f, 3f),
            Constant(1, 4f, 5f, 6f, 7f),
            ConstantInt(0, 3, 0, 1, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 1, 1),
            Instruction(Opcode.Mul, Rd(0), V(0), C(1)),
            Instruction(Opcode.Mov, Rd(3), C(0, X)),
            Instruction(Opcode.Rep, Source(RegisterType.ConstInt, 0)),
            Instruction(Opcode.Add, Rd(3), R(3), R(0)),                       // read above the if
            Instruction(Opcode.IfC, IfComparison.LT, V(1, X, SourceModifier.Negate), C(0, X)),
            Instruction(Opcode.Mov, Rd(0), C(0)),
            Instruction(Opcode.Else),
            Instruction(Opcode.Cmp, Rd(0), R(0, X, SourceModifier.Negate), C(0), C(1)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.EndRep),
            Instruction(Opcode.Add, Oc(), R(0), R(3)),                       // read after the loop
            Instruction(Opcode.End),
        ]);

        AssertDecompiles(shader, """
            struct PS_IN
            {
            	float4 texcoord : TEXCOORD;
            	float4 texcoord1 : TEXCOORD1;
            };

            float4 main(PS_IN i) : COLOR
            {
            	float4 t0 = float4(4, 5, 6, 7) * i.texcoord;
            	float4 t1 = 0;
            	for (int i_ = 0; i_ < 3; i_++) {
            		t1 = t1 + t0;
            		if (i.texcoord1.x > 0) {
            			t0 = float4(0, 1, 2, 3);
            		} else {
            			t0 = -t0.x >= 0 ? float4(0, 1, 2, 3) : float4(4, 5, 6, 7);
            		}
            	}
            	return t0 + t1;
            }

            """);
    }

    // The text, then fxc's compile of it computes what the shader does.
    internal static void AssertDecompiles(ShaderModel shader, string expected)
    {
        string hlsl = WriteHlslAst(shader);
        Console.WriteLine(hlsl);

        Assert.Multiple(() =>
        {
            Assert.That(hlsl, Is.EqualTo(expected));
            if (ShaderCompiler.IsAvailable)
            {
                string path = Path.Combine(TestContext.CurrentContext.TestDirectory,
                    TestContext.CurrentContext.Test.ClassName.Split('.')[^1] + "_" + TestContext.CurrentContext.Test.MethodName + ".hlsl");
                File.WriteAllText(path, hlsl);
                string failure = RecompileTests.RunFxc("ps_3_0", path, path + ".fxo");
                Assert.That(failure, Is.Null);
                if (failure == null)
                {
                    ShaderModel recompiled = RecompileTests.ReadShaderModel(path + ".fxo");
                    Assert.That(EquivalenceTests.CompareRuns(shader, recompiled, "the original", "its decompilation")
                        .ToList(), Is.Empty);
                }
            }
        });
    }
}
