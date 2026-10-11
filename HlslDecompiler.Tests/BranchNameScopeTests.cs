using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A value computed before an if and read by what both of its branches return is
// one node read twice through one reader - the two returns hold the same
// expression - and naming it while writing the first branch rewires that shared
// reader, so the second branch compiled its text naming too: X3004 at a name
// whose declaration closed its brace one line before. The naming pass only gives
// the variable to readers of the statement it is writing; it has to notice that
// the graph it shares reaches a statement the text pass will write after this
// one in a place the name will not reach. fxc never compiles HLSL into the shape
// - it would have named the value above the branch - so it is assembled by hand,
// after a vehicle dirt pass that read one sample's .xyz above the branch and its
// .w through both of them.
public class BranchNameScopeTests
{
    private const uint X = 0x00;    // .x replicated
    private const uint Z = 0xAA;    // .z replicated
    private const uint W = 0xFF;    // .w replicated
    private const uint XYZ = 0x24;  // .xyz

    [Test]
    public void DoesNotNameAValueBeforeASiblingBranchReadsIt()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0f, 1f, 0f, 0f),
            Constant(1, 0.5f, 0.25f, 0.75f, 0.5f),
            Constant(2, 0.8f, 0.3f, 0.1f, 0f),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0, 10),
            Declaration(DeclUsage.Color, RegisterType.Input, 1),
            Declaration(DeclUsage.Color, RegisterType.Output, 0),
            SamplerDeclaration(SamplerTextureType.TwoD, 0),
            SamplerDeclaration(SamplerTextureType.TwoD, 1),
            Instruction(Opcode.Tex, Rd(0), V(0), Source(RegisterType.Sampler, 0)),  // texld r0, v0, s0
            Instruction(Opcode.Mul, Rd(1, 0x7), R(0, XYZ), C(1, XYZ)),              // r1.xyz = r0.xyz * K
            Instruction(Opcode.Mul, Rd(2, 0x7), R(1, XYZ), V(1, XYZ)),              // r2.xyz = r1 * color
            Instruction(Opcode.Mul, Rd(0, 0x8), R(0, W), C(1, X)),                  // r0.w = r0.w * K2
            Instruction(Opcode.Mul, Rd(0, 0x8), R(0, W), V(0, W)),                  // times another read: the .w
            Instruction(Opcode.Mul, Od(0, 0x8), R(0, W), V(1, W)),                  // oC0.w = r0.w * color.w
            Instruction(Opcode.Mov, Rd(3, 0x1), C(1, Z)),                           // dirtLevel
            Instruction(Opcode.IfC, IfComparison.LT,
                R(3, X, SourceModifier.Negate), C(0, X)),                           // if dirtLevel > 0
            Instruction(Opcode.Tex, Rd(3), V(0), Source(RegisterType.Sampler, 1)),  // texld r3, v0, s1
            Instruction(Opcode.Mul, Rd(0, 0x8), R(3, X), C(1, Z)),                  // r0.w = r3.x * dirtLevel
            Instruction(Opcode.Mad, Rd(1, 0x7), R(1, XYZ), V(1, XYZ, SourceModifier.Negate), C(2, XYZ)),
            Instruction(Opcode.Mad, Rd(1, 0x7), R(0, W), R(1, XYZ), R(2, XYZ)),     // r0.www blend
            Instruction(Opcode.Cmp, Od(0, 0x7),
                R(3, X, SourceModifier.Negate), R(2, XYZ), R(1, XYZ)),
            Instruction(Opcode.Else),
            Instruction(Opcode.Mov, Od(0, 0x7), R(2, XYZ)),
            Instruction(Opcode.Endif),
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteAsm(shader), Is.EqualTo("""
                ps_3_0
                def c0, 0, 1, 0, 0
                def c1, 0.5, 0.25, 0.75, 0.5
                def c2, 0.800000012, 0.300000012, 0.100000001, 0
                dcl_texcoord10 v0
                dcl_color v1
                dcl_color o0
                dcl_2d s0
                dcl_2d s1
                texld r0, v0.xy, s0
                mul r1.xyz, r0.xyz, c1.xyz
                mul r2.xyz, r1.xyz, v1.xyz
                mul r0.w, r0.w, c1.x
                mul r0.w, r0.w, v0.w
                mul o0.w, r0.w, v1.w
                mov r3.x, c1.z
                if_lt -r3.x, c0.x
                texld r3, v0.xy, s1
                mul r0.w, r3.x, c1.z
                mad r1.xyz, r1.xyz, -v1.xyz, c2.xyz
                mad r1.xyz, r0.www, r1.xyz, r2.xyz
                cmp o0.xyz, -r3.xxx, r2.xyz, r1.xyz
                else
                mov o0.xyz, r2.xyz
                endif

                """));
            // The first branch cannot name the shared .w - the else branch reads
            // the same reader node and would then name an undeclared thing - so it
            // writes the fetch out where it reads it, and only the last reader,
            // which nothing can follow, gets the variable.
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                sampler2D s0;
                sampler2D s1;

                struct PS_IN
                {
                	float4 texcoord10 : TEXCOORD10;
                	float4 color : COLOR;
                };

                float4 main(PS_IN i) : COLOR
                {
                	float3 t1 = tex2D(s0, i.texcoord10.xy).xyz;
                	float3 t0 = float3(0.5, 0.25, 0.75) * t1;
                	float3 t2 = t0 * i.color.xyz;
                	if (-0.75 < 0) {
                		float t3 = tex2D(s1, i.texcoord10.xy).x;
                		return float4(-t3 >= 0 ? t2 : 0.75 * t3 * (t0 * -i.color.xyz + float3(0.800000012, 0.300000012, 0.100000001)) + t2, 0.5 * tex2D(s0, i.texcoord10.xy).w * i.texcoord10.w * i.color.w);
                	} else {
                		float t4 = tex2D(s0, i.texcoord10.xy).w;
                		return float4(t2, 0.5 * t4 * i.texcoord10.w * i.color.w);
                	}
                }

                """), WriteHlslAst(shader));
        });
    }
}
