using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using static HlslDecompiler.Tests.ShaderAssembler;

namespace HlslDecompiler.Tests;

// The two writes of one register are two statements, never two lanes of one
// variable: a value built on a named candidate is that candidate read again, not
// a sibling component - and grouping the pair anyway computes the second lane
// from the first at the first reader, gluing both assignment statements inside a
// float2 constructor and leaving the variable undeclared. X3000 at that
// constructor and X3004 at every read of the temp after. fxc folds this shape
// away from HLSL - a register's double write is a scheduling artefact - so no
// golden can hold one: assembled by hand. The dp3 is worth naming - over an
// expression too big to write out twice, and read twice itself - and its double
// is written over it with what compiles to a constant times it, which is the
// folded factor the grouper looks through to the component it is. The double is
// read twice too, so both are candidates in one round.
public class ReassignedTempGroupingTests
{
    private const uint X = 0x00;    // .x replicated
    private const uint Y = 0x55;    // .y replicated
    private const uint Z = 0xAA;    // .z replicated
    private const uint XYZ = 0x24;  // .xyz

    [Test]
    public void NamesTheTwoWritesOfOneRegisterApart()
    {
        ShaderModel shader = Assemble(ShaderType.Pixel, 3, [
            Constant(0, 0.25f, 0.5f, 0.75f, 2f),
            Constant(1, 0.1f, 0.2f, 0.3f, 0f),
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Declaration(DeclUsage.Color, RegisterType.Output, 0),
            // the dot's operand: an expression, so the dot is no one's shortcut
            Instruction(Opcode.Mul, Rd(1, 0x7), V(0, XYZ), C(1, XYZ)),
            Instruction(Opcode.Add, Rd(1, 0x7), R(1, XYZ), V(0, Z, SourceModifier.Negate)),
            Instruction(Opcode.Dp3, Rd(0, 0x1), R(1, XYZ), C(0, XYZ)),           // the dot
            Instruction(Opcode.Mul, Rd(1, 0x7), V(0, XYZ), R(0, X)),             // read it
            Instruction(Opcode.Add, Rd(0, 0x1), R(0, X), R(0, X)),               // double it, in place
            Instruction(Opcode.Mul, Rd(2, 0x7), R(1, XYZ), R(0, X)),             // and read the double
            Instruction(Opcode.Add, Rd(3, 0x7), R(1, XYZ), R(0, X)),             // twice
            Instruction(Opcode.Mad, Od(0, 0x7), R(2, XYZ), R(3, XYZ), C(1, XYZ)),
            Instruction(Opcode.Mov, Od(0, 0x8), C(0, Y)),
            Instruction(Opcode.End),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(WriteAsm(shader), Is.EqualTo("""
                ps_3_0
                def c0, 0.25, 0.5, 0.75, 2
                def c1, 0.100000001, 0.200000003, 0.300000012, 0
                dcl_texcoord v0
                dcl_color o0
                mul r1.xyz, v0.xyz, c1.xyz
                add r1.xyz, r1.xyz, -v0.zzz
                dp3 r0.x, r1.xyz, c0.xyz
                mul r1.xyz, v0.xyz, r0.xxx
                add r0.x, r0.x, r0.x
                mul r2.xyz, r1.xyz, r0.xxx
                add r3.xyz, r1.xyz, r0.xxx
                mad o0.xyz, r2.xyz, r3.xyz, c1.xyz
                mov o0.w, c0.y

                """));
            Assert.That(WriteHlslAst(shader), Is.EqualTo("""
                float4 main(float4 texcoord : TEXCOORD) : COLOR
                {
                	float t0 = dot(float3(0.25, 0.5, 0.75), float3(0.100000001, 0.200000003, 0.300000012) * texcoord.xyz - texcoord.z);
                	float3 t1 = texcoord.xyz * t0;
                	float t2 = 2 * t0;
                	return float4(t1 * t2 * (t1 + t2) + float3(0.100000001, 0.200000003, 0.300000012), 0.5);
                }

                """), WriteHlslAst(shader));
        });
    }

    // A lane whose value is built on another lane's new value is no write beside
    // it, whatever the shapes say: the record the lowering left names exactly
    // that, and the grouper asks it against every member of the group it is
    // about to extend - the first of them included. A vertex shader's
    // texture unpack wrote ch1, ch2 and ch3 into one register, ch3 built on ch1
    // through a rest of its own; ch1 and ch3 were two adds of a product and
    // grouped into one `t4.zx = float2(...)`, the fraction between them keeping
    // ch1 the group's first lane - and the pair read a value the one statement
    // had yet to write, the variable they shared declared below the read of ch1.
    // Asked at node level: fxc only schedules a chain this deep in a register it
    // met in the wild, and the naming passes above the grouper inline the whole
    // of a hand-assembled one.
    [Test]
    public void DoesNotGroupALaneWithTheFirstLaneWhoseValueItReads()
    {
        var grouper = new NodeGrouper(new RegisterState(Assemble(ShaderType.Pixel, 3, [
            Declaration(DeclUsage.TexCoord, RegisterType.Input, 0),
            Declaration(DeclUsage.Color, RegisterType.Output, 0),
            Instruction(Opcode.End),
        ])));

        static TempVariableNode Lane(int component) => new()
        {
            DeclarationIndex = 4, ComponentIndex = component, VariableSize = 3,
        };
        static TempVariableNode Read(int index, int component) => new()
        {
            DeclarationIndex = index, ComponentIndex = component, VariableSize = 1,
        };
        // Two adds of reads' rows group on their shapes alone - the pair below
        // is the same with a record between them.
        var shapes = grouper.GroupComponents([
            new TempAssignmentNode(Lane(2), new AddOperation(Read(1, 0), Read(2, 0))),
            new TempAssignmentNode(Lane(0), new AddOperation(Read(1, 2), Read(2, 2))),
        ]);
        Assert.That(shapes.Count, Is.EqualTo(1),
            "without a record the two adds group, or the generations ask nothing");

        var ch1 = new TempAssignmentNode(Lane(2), new AddOperation(Read(1, 0), Read(2, 0)));
        var ch3 = new TempAssignmentNode(Lane(0), new AddOperation(Read(1, 2), Read(2, 2)));
        ch3.DependsOnNewValueOf.Add(ch1);
        var generations = grouper.GroupComponents([ch1, ch3]);
        Assert.That(generations.Single(g => g.Contains(ch1, ReferenceEqualityComparer.Instance)).Count,
            Is.EqualTo(1),
            "ch1 is written where its reader can see it, alone and not beside ch3");
    }
}
