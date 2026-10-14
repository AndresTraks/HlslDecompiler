using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// A named value must be declared before every line that reads it. Hard case: a
// sincos pair overwritten component by component with values built from it, all
// shared, one reading another. Hand-assembled: fxc reassociates the shape away.
public class TempDeclarationOrderTests
{
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

    // The same pair across a whole statement ended by a loop, so the lerp chain's
    // reader lands after it; the cross product must not precede the pair.
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
            // r0 and r1 are each written several times below.
            Instruction(Opcode.SinCos, Rd(1, 0x3), V(1, Z)),              // sincos r1.xy, v1.zz
            // the products, then the two overwrites: the cross product and the dot.
            Instruction(Opcode.Mul, Rd(0, 0xE), R(1, YYX), V(1, YXY)),    // mul r0.yzw, r1.yyx, v1.yxy
            Instruction(Opcode.Mad, Rd(1, 0x1), V(1, X), R(1, X),         // mad r1.x, v1.x, r1.x, -r0.y
                R(0, Y, SourceModifier.Negate)),
            Instruction(Opcode.Add, Rd(1, 0x2), R(0, W), R(0, Z)),        // add r1.y, r0.w, r0.z
            Instruction(Opcode.Mov, Rd(1, 0x4), C(0, X)),                 // mov r1.z, c0.x
            // feeds the partial position write; .w is filled later.
            Instruction(Opcode.Mul, Rd(0, 0xE), R(0, X), R(1, XYZ)),     // mul r0.yzw, r0.xxx, r1.xyz
            Instruction(Opcode.Mad, Od(0, 0x7), V(0), C(0, WWZ,          // mad o0.xyz, v0.xyz, c0_abs.wwz, r0.yzw
                SourceModifier.Abs), R(0, YZW)),
            // the lerp's operand: both built-on values read a second time.
            Instruction(Opcode.Add, Rd(1, 0x7), R(1), V(0, 0xE4, SourceModifier.Negate)), // add r1.xyz, r1.xyz, -v0.xyz
            // The lerp ends the first statement; its reader is after the loop.
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

    // A root vector is not named when a reader of one lane builds another lane
    // (here w = dot of a multiply over xyz): that is a cycle no statement order
    // can write. The row coefficients of 1 stop the products fusing into dots.
    [Test]
    public void LeavesTheRootUnnamedWhenItsLaneReadersBuildAnotherLane()
    {
        const uint Y = 0x55;    // .y replicated
        const uint XY = 0x04;   // slots x, y = .x, .y
        const uint XYZ = 0x24;  // .xyz
        const uint XYZX = 0x24; // .xyzx
        const uint YYYZ = 0xE5; // .yyyz
        const uint ZZZY = 0x6A; // .zzzy
        const uint W = 0xFF;    // .w replicated

        ShaderModel shader = Assemble(ShaderType.Vertex, 3, [
            Constant(0, 1f, 2f, 3f, 4f),                              // lane offsets, w offset
            Constant(1, 5f, 6f, 1f, 8f),                              // multiply row y
            Constant(2, 1f, 10f, 11f, 12f),                           // multiply row x
            Constant(3, 13f, 1f, 15f, 16f),                           // multiply row z
            Constant(4, 17f, 18f, 19f, 20f),                          // multiply's last row
            Constant(5, 2f, 3f, 0f, 0f),                              // the w dot's weights
            Constant(6, 1f, 1f, 0.5f, 3f),                            // the dot input's weights
            Constant(7, 3f, 2f, 1f, 4f),                              // the dot's row x
            Constant(8, 4f, 3f, 2f, 1f),                              // the dot's row y
            Constant(9, 2f, 4f, 3f, 1f),                              // the dot's row z
            Declaration(DeclUsage.Position, RegisterType.Input, 0),
            Declaration(DeclUsage.Position, RegisterType.Output, 0),
            Declaration(DeclUsage.TexCoord, RegisterType.Output, 1, 1),
            Declaration(DeclUsage.TexCoord, RegisterType.Output, 2, 2),
            Instruction(Opcode.Mad, Rd(1), V(0, XYZX), C(6, YYYZ), C(6, ZZZY)), // the shared dot input
            Instruction(Opcode.Dp4, Rd(2, 0x1), R(1), C(7)),           // its three dots
            Instruction(Opcode.Dp4, Rd(2, 0x2), R(1), C(8)),
            Instruction(Opcode.Dp4, Rd(2, 0x4), R(1), C(9)),
            Instruction(Opcode.Add, Rd(0, 0x7), R(2, XYZ), C(0, XYZ)), // the root's lanes
            Instruction(Opcode.Mul, Rd(3, 0x7), R(0, Y), C(1, XYZ)),   // the product, a lane at a time
            Instruction(Opcode.Mad, Rd(3, 0x7), R(0, X), C(2, XYZ), R(3, XYZ)),
            Instruction(Opcode.Mad, Rd(3, 0x7), R(0, Z), C(3, XYZ), R(3, XYZ)),
            Instruction(Opcode.Add, Rd(3, 0x7), R(3, XYZ), C(4, XYZ)), // and its last row
            Instruction(Opcode.Mov, Od(1, 0x7), R(0, XYZ)),            // the root's three first lanes
            Instruction(Opcode.Mul, Rd(4, 0x3), R(3, XY), C(5, XY)),   // the dot the w lane is built on
            Instruction(Opcode.Add, Rd(4, 0x1), R(4, Y), R(4, X)),
            Instruction(Opcode.Add, Rd(0, 0x8), R(4, X), C(0, W)),     // the w lane, over the dot
            Instruction(Opcode.Mov, Od(1, 0x8), R(0, W)),              // a read of the w the fold hides
            Instruction(Opcode.Mul, Od(0, 0x7), R(3, XYZ), C(5, Y)),   // the product read whole, scaled
            Instruction(Opcode.Mov, Od(0, 0x8), R(0, W)),              // the w read once
            Instruction(Opcode.Mov, Od(2, 0x1), R(0, W)),              // and twice
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                struct VS_OUT
                {
                	float4 position : POSITION;
                	float4 texcoord1 : TEXCOORD1;
                	float4 texcoord2 : TEXCOORD2;
                };

                VS_OUT main(float4 position : POSITION)
                {
                	VS_OUT o;

                	float4 t0 = position.xyzx * float4(1, 1, 0.5, 3) + float4(0.5, 0.5, 0.5, 1);
                	float t1 = dot(float2(3, 2), t0.xy) + t0.z + 4 * t0.w;
                	float t2 = dot(float4(4, 3, 2, 1), t0);
                	float t3 = dot(float4(2, 4, 3, 1), t0);
                	float2 t4 = float2(t1 + 1 + 5 * (t2 + 2) + 13 * (t3 + 3), t3 + 3 + (10 * (t1 + 1) + 6 * (t2 + 2))) + float2(17, 18);
                	float t5 = 3 * t4.y + 2 * t4.x;
                	o.position = float4(3 * t4, 3 * (t2 + 2 + 11 * (t1 + 1) + 15 * (t3 + 3) + 19), t5 + 4);
                	o.texcoord1 = float4(t1, t2, t3, t5) + float4(1, 2, 3, 4);
                	o.texcoord2.x = t5 + 4;

                	return o;
                }

                """), WriteHlslAst(shader));
        });
    }
}
