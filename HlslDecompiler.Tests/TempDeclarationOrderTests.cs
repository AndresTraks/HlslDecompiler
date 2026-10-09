using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A value the writer names has to be declared before the lines that read it, shared
// lines included. A `sincos` whose results are then overwritten component by component
// is the hard case: the cosine and the sine are each read twice more through the
// products folded into the values built over them, so the pair comes back as one
// shared `float2` and those built-on values are shared in their turn - and one of
// them reads the other, so the shared line for the pair cannot simply go first.
// fxc reassociates this shape away whenever HLSL writes it, so no golden can hold
// one: assembled by hand from the binary that triggered it - the products of the
// trig pair, a mad that overwrites the cosine with a value built from it, an add
// that overwrites the sine with a dot over the pair, and each of the two built-on
// values read twice.
public class TempDeclarationOrderTests
{
    // The swizzles both fixtures read: the four components, replicated, and the
    // two three-wide ones a sincos pair's componentwise products with a vector.
    private const uint X = 0x00;    // .x replicated
    private const uint Y = 0x55;    // .y replicated
    private const uint Z = 0xAA;    // .z replicated
    private const uint W = 0xFF;    // .w replicated
    private const uint YYX = 0x14;  // slots y, z, w = .y, .y, .x
    private const uint YXY = 0x44;  // slots y, z, w = .y, .x, .y

    [Test]
    public void OrdersASharedTrigPairBeforeTheValuesBuiltFromIt()
    {
        const uint XW = 0x0C;  // slots x, y = .x, .w

        ShaderModel shader = Assemble(ShaderType.Vertex, 3, [
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),     // dcl_texcoord v0
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 1, 1),  // dcl_texcoord1 v1
            Declaration(DeclUsage.Position, RegisterType.Output, 0),    // dcl_position o0
            Constant(0, 5.0f, 6.0f, 7.0f, 8.0f),
            Instruction(Opcode.Frc, Rd(0, 0x2), V(0, Z)),               // frc r0.y, v0.z
            // sincos r1.xy, r0.y - r1.x = cos(a), r1.y = sin(a)
            Instruction(Opcode.SinCos, Rd(1, 0x3), R(0, Y)),
            // the products of the trig pair: sin*n.y, sin*n.x, cos*n.y
            Instruction(Opcode.Mul, Rd(0, 0xE), R(1, YYX), V(0, YXY)),  // mul r0.yzw, r1.yyx, v0.yxy
            // mad r1.x, v0.x, r1.x, -r0.y - overwrites the cosine with n.x*cos - sin*n.y
            Instruction(Opcode.Mad, Rd(1, 0x1), V(0, X), R(1, X),
                R(0, Y, SourceModifier.Negate)),
            // add r1.y, r0.w, r0.z - overwrites the sine with cos*n.y + sin*n.x
            Instruction(Opcode.Add, Rd(1, 0x2), R(0, W), R(0, Z)),
            // each of the two built-on values read twice
            Instruction(Opcode.Mul, Rd(8, 0x3), V(1, XW), R(1)),        // mul r8.xy, v1.xw, r1.xy
            Instruction(Opcode.Mad, Od(0, 0x3), R(1), C(0), R(8)),      // mad o0.xy, r1.xy, c0.xy, r8.xy
            Instruction(Opcode.Mov, Od(0, 0xC), C(0)),                  // mov o0.zw, c0.zw
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteAsm(shader), Is.EqualTo("""
                vs_3_0
                dcl_texcoord v0
                dcl_texcoord1 v1
                dcl_position o0
                def c0, 5, 6, 7, 8
                frc r0.y, v0.z
                sincos r1.xy, r0.yy
                mul r0.yzw, r1.yyx, v0.yxy
                mad r1.x, v0.x, r1.x, -r0.y
                add r1.y, r0.w, r0.z
                mul r8.xy, v1.xw, r1.xy
                mad o0.xy, r1.xy, c0.xy, r8.xy
                mov o0.zw, c0.zw

                """));
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                struct VS_IN
                {
                	float4 texcoord : TEXCOORD;
                	float4 texcoord1 : TEXCOORD1;
                };

                float4 main(VS_IN i) : POSITION
                {
                	float t0 = frac(i.texcoord.z);
                	float2 t1 = float2(cos(t0), sin(t0));
                	float t2 = i.texcoord.x * t1.x - t1.y * i.texcoord.y;
                	float t3 = dot(t1, i.texcoord.yx);
                	return float4(5 * t2 + i.texcoord1.x * t2, 6 * t3 + i.texcoord1.w * t3, 7, 8);
                }

                """));
        });
    }

    // A binary's statement block that actually mis-ordered, boiled down to the
    // instructions the bug needs: the trig pair, its products, the mad and add
    // that overwrite the two results, a lerp chain reading both values built over
    // them, a partial position write, and a loop ending the statement so the
    // chain's reader lands in the block after it. The named shared values used to
    // land in the wrong order: the cross product before the pair it reads.
    [Test]
    public void OrdersTheTrigPairAmongTheNamedValuesOfTheWholeStatement()
    {
        const uint XYZ = 0x90; // slots y, z, w = .x, .y, .z
        const uint WWZ = 0x2F; // slots x, y, z = .w, .w, .z
        const uint YZW = 0x39; // slots x, y, z = .y, .z, .w

        ShaderModel shader = Assemble(ShaderType.Vertex, 3, [
            Constant(0, 0.0f, 0.0f, 0.0f, -1.0f),                  // c0
            ConstantInt(0, 9, 0, 1, 0),
            Declaration(DeclUsage.Position, RegisterType.Input, 0),   // dcl_position v0
            Declaration(DeclUsage.Normal, RegisterType.Input, 1),     // dcl_normal v1
            Declaration(DeclUsage.Position, RegisterType.Output, 0),  // dcl_position o0
            Declaration(DeclUsage.Color, RegisterType.Output, 1),     // dcl_color o1
            // a step - the lerp weight - built from v1.w, read twice
            Instruction(Opcode.Add, Rd(0, 0x1), V(1, W), C(0, W)),       // add r0.x, v1.w, c0.w
            Instruction(Opcode.Sge, Rd(0, 0x1),                           // sge r0.x, -r0_abs.x, r0_abs.x
                R(0, X, SourceModifier.AbsAndNegate), R(0, X, SourceModifier.Abs)),
            // sincos r1.xy, v1.z: r0 and r1 are each written several times below,
            // which is what the old sort tripped on.
            Instruction(Opcode.SinCos, Rd(1, 0x3), V(1, Z)),              // sincos r1.xy, v1.zz
            // the products, then the two overwrites: the cross product and the dot.
            Instruction(Opcode.Mul, Rd(0, 0xE), R(1, YYX), V(1, YXY)),    // mul r0.yzw, r1.yyx, v1.yxy
            Instruction(Opcode.Mad, Rd(1, 0x1), V(1, X), R(1, X),         // mad r1.x, v1.x, r1.x, -r0.y
                R(0, Y, SourceModifier.Negate)),
            Instruction(Opcode.Add, Rd(1, 0x2), R(0, W), R(0, Z)),        // add r1.y, r0.w, r0.z
            Instruction(Opcode.Mov, Rd(1, 0x4), C(0, X)),                 // mov r1.z, c0.x
            // the step times the cross product, the dot and their zero, feeding
            // the partial position write - whose .w the second statement fills.
            Instruction(Opcode.Mul, Rd(0, 0xE), R(0, X), R(1, XYZ)),     // mul r0.yzw, r0.xxx, r1.xyz
            Instruction(Opcode.Mad, Od(0, 0x7), V(0), C(0, WWZ,          // mad o0.xyz, v0.xyz, c0_abs.wwz, r0.yzw
                SourceModifier.Abs), R(0, YZW)),
            // the lerp's operand: both built-on values read a second time.
            Instruction(Opcode.Add, Rd(1, 0x7), R(1), V(0, 0xE4, SourceModifier.Negate)), // add r1.xyz, r1.xyz, -v0.xyz
            // The lerp ends the first statement; the loop that follows is the
            // second, and the loop's reader of the lerp comes after it - so the
            // shared lines of the whole statement, the pair first among them,
            // all have to be named before the loop.
            Instruction(Opcode.Mad, Rd(0, 0x7), R(0, X), R(1), V(0)),     // mad r0.xyz, r0.xxx, r1.xyz, v0.xyz
            Instruction(Opcode.Mov, Rd(2, 0x1), C(0, X)),                 // mov r2.x, c0.x
            Instruction(Opcode.Loop, Source(RegisterType.Loop, 14),       // loop aL, i0
                Source(RegisterType.ConstInt, 0)),
            Instruction(Opcode.Add, Rd(2, 0x1), R(2, X), C(0, W)),        // add r2.x, r2.x, c0.w
            Instruction(Opcode.EndLoop),
            Instruction(Opcode.Add, Od(1, 0x8), R(2, X), C(0, W)),        // add o1.w, r2.x, c0.w - the loop's result
            Instruction(Opcode.Mov, Od(1, 0x7), R(0)),                    // mov o1.xyz, r0.xyz - the lerp, read after
                                                                          // the loop, keeping its chain alive
            Instruction(Opcode.Mov, Od(0, 0x8), C(0, W, SourceModifier.Negate)), // mov o0.w, -c0.w
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteAsm(shader), Is.EqualTo("""
                vs_3_0
                def c0, 0, 0, 0, -1
                defi i0, 9, 0, 1, 0
                dcl_position v0
                dcl_normal v1
                dcl_position o0
                dcl_color o1
                add r0.x, v1.w, c0.w
                sge r0.x, -r0_abs.x, r0_abs.x
                sincos r1.xy, v1.zz
                mul r0.yzw, r1.yyx, v1.yxy
                mad r1.x, v1.x, r1.x, -r0.y
                add r1.y, r0.w, r0.z
                mov r1.z, c0.x
                mul r0.yzw, r0.xxx, r1.xyz
                mad o0.xyz, v0.xyz, c0_abs.wwz, r0.yzw
                add r1.xyz, r1.xyz, -v0.xyz
                mad r0.xyz, r0.xxx, r1.xyz, v0.xyz
                mov r2.x, c0.x
                loop aL, i0
                add r2.x, r2.x, c0.w
                endloop
                add o1.w, r2.x, c0.w
                mov o1.xyz, r0.xyz
                mov o0.w, -c0.w

                """));
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                struct VS_IN
                {
                	float4 position : POSITION;
                	float4 normal : NORMAL;
                };

                struct VS_OUT
                {
                	float4 position : POSITION;
                	float4 color : COLOR;
                };

                VS_OUT main(VS_IN i)
                {
                	VS_OUT o;

                	float t4 = step(abs(i.normal.w - 1), -abs(i.normal.w - 1));
                	float t0 = t4;
                	float2 t1 = float2(cos(i.normal.z), sin(i.normal.z));
                	float t5 = i.normal.x * t1.x - t1.y * i.normal.y;
                	float3 t2 = float3(lerp(i.position.x, t5, t0), lerp(i.position.y, dot(t1, i.normal.yx), t0), t0 * -i.position.z + i.position.z);
                	float t3 = 0;
                	o.position.xyz = float3(i.position.x + t4 * t5, i.position.y + t4 * dot(t1, i.normal.yx), 0);
                	for (int i_ = 0; i_ < 9; i_++) {
                		t3 = t3 - 1;
                	}
                	o.position.w = 1;
                	o.color = float4(t2, t3 - 1);

                	return o;
                }

                """));
        });
    }
}
