using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Tests.Interpreter;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static HlslDecompiler.Tests.DxbcShaderAssembler;
using OperandType = HlslDecompiler.DirectXShaderModel.OperandType;

namespace HlslDecompiler.Tests;

// DXBC instructions fxc never compiles from HLSL (the docs confirm fxc inlines
// every call and folds 64-bit arithmetic away). Expected text is fxc /dumpbin's
// listing of the same bytes (WritesTheBytesForDumpbin), as house-styled
// elsewhere - except labels, which the spec pages write l# and fxc label#. The
// machine cases run the hand-assembled shader against the interpreter because
// the equivalence tier can never reach these shapes; WARP agrees with them
// (tools/warp-probe, ucarry_probe).
public class AssemblyOnlyDxbcInstructionTests
{
    [Test]
    public void DisassemblesTheInstructionsNoHlslCompilesInto()
    {
        ShaderModel shader = Assemble(ShaderType.Compute, 5, 0, [
            Declaration(D3D10Opcode.DclUnorderedAccessViewStructured,
                Register(OperandType.UnorderedAccessView, 0), 16),
            Declaration(D3D10Opcode.DclTemps, 4),
            Declaration(D3D10Opcode.DclThreadGroup, 1, 1, 1),
            Instruction(D3D10Opcode.Mov, Destination(OperandType.Temp, 0),
                Immediate(0xFFFFFFFF, 3, 0, 0x80000000)),
            // umul high and low; either may be null.
            Instruction(D3D10Opcode.UMul, Destination(OperandType.Temp, 1),
                Destination(OperandType.Temp, 2), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 0)),
            Instruction(D3D10Opcode.UMul, Null(), Destination(OperandType.Temp, 3),
                Source(OperandType.Temp, 0), Source(OperandType.Temp, 0)),
            // uaddc/usubb sum-and-carry, difference-and-borrow; carry/borrow may be null.
            Instruction(D3D10Opcode.UAddC, Destination(OperandType.Temp, 1),
                Destination(OperandType.Temp, 2), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 0)),
            Instruction(D3D10Opcode.UAddC, Destination(OperandType.Temp, 1), Null(),
                Source(OperandType.Temp, 0), Source(OperandType.Temp, 3)),
            Instruction(D3D10Opcode.USubB, Destination(OperandType.Temp, 1),
                Destination(OperandType.Temp, 2), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 3)),
            // swapc over two different destination masks.
            Instruction(D3D10Opcode.SwapC, Destination(OperandType.Temp, 1, 0x3),
                Destination(OperandType.Temp, 2, 0xC), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 3), Source(OperandType.Temp, 1)),
            Instruction(D3D10Opcode.Nop),
            Instruction(D3D10Opcode.Call, Label(2)),
            InstructionWithTestBit(true, D3D10Opcode.CallC,
                Component(OperandType.Temp, 0, 0), Label(2)),
            Instruction(D3D10Opcode.Label, Label(2)),
            Instruction(D3D10Opcode.Ret),
        ]);

        Assert.That(WriteAsm(shader), Is.EqualTo("""
            cs_5_0
            dcl_uav_structured u0, 16
            dcl_temps 4
            dcl_thread_group 1, 1, 1
            mov r0, l(-1, 3, 0, -2147483648)
            umul r1, r2, r0, r0
            umul null, r3, r0, r0
            uaddc r1, r2, r0, r0
            uaddc r1, null, r0, r3
            usubb r1, r2, r0, r3
            swapc r1.xy, r2.zw, r0, r3, r1
            nop
            call label2
            callc_nz r0.x, label2
            label label2
            ret

            """));
    }

    // Writes the bytes for `fxc /nologo /dumpbin` to list. d3dcompiler and the
    // D3D11 runtime refuse a container without the right checksum, which
    // AssembleBytes now computes (DxbcChecksum). fxc's listing differs from the
    // one above only in house style: full swizzles, and the mov's 0x80000000
    // written as the float -0.000000.
    [Test]
    [Explicit("Writes the hand-assembled shader to %TEMP% for fxc /dumpbin.")]
    public void WritesTheBytesForDumpbin()
    {
        string path = Path.Combine(Path.GetTempPath(), "opencode", "dxbc_assembly_only.fxc");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, AssembleBytes(ShaderType.Compute, 5, 0, [
            Declaration(D3D10Opcode.DclUnorderedAccessViewStructured,
                Register(OperandType.UnorderedAccessView, 0), 16),
            Declaration(D3D10Opcode.DclTemps, 4),
            Declaration(D3D10Opcode.DclThreadGroup, 1, 1, 1),
            Instruction(D3D10Opcode.Mov, Destination(OperandType.Temp, 0),
                Immediate(0xFFFFFFFF, 3, 0, 0x80000000)),
            Instruction(D3D10Opcode.UMul, Destination(OperandType.Temp, 1),
                Destination(OperandType.Temp, 2), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 0)),
            Instruction(D3D10Opcode.UMul, Null(), Destination(OperandType.Temp, 3),
                Source(OperandType.Temp, 0), Source(OperandType.Temp, 0)),
            Instruction(D3D10Opcode.UAddC, Destination(OperandType.Temp, 1),
                Destination(OperandType.Temp, 2), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 0)),
            Instruction(D3D10Opcode.UAddC, Destination(OperandType.Temp, 1), Null(),
                Source(OperandType.Temp, 0), Source(OperandType.Temp, 3)),
            Instruction(D3D10Opcode.USubB, Destination(OperandType.Temp, 1),
                Destination(OperandType.Temp, 2), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 3)),
            Instruction(D3D10Opcode.SwapC, Destination(OperandType.Temp, 1, 0x3),
                Destination(OperandType.Temp, 2, 0xC), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 3), Source(OperandType.Temp, 1)),
            Instruction(D3D10Opcode.Nop),
            Instruction(D3D10Opcode.Call, Label(2)),
            InstructionWithTestBit(true, D3D10Opcode.CallC,
                Component(OperandType.Temp, 0, 0), Label(2)),
            Instruction(D3D10Opcode.Label, Label(2)),
            Instruction(D3D10Opcode.Ret),
        ]));
        TestContext.WriteLine(path);
    }

    // Operand pairs, one instruction per element of the UAV; stored to disk too
    // for tools/warp-probe to run against the interpreter.
    private static uint[][] CarryBorrowSwapInstructions()
    {
        const uint factor1 = 0xFFFFFFFF;
        return [
            Declaration(D3D10Opcode.DclUnorderedAccessViewStructured,
                Register(OperandType.UnorderedAccessView, 0), 16),
            Declaration(D3D10Opcode.DclTemps, 16),
            Declaration(D3D10Opcode.DclThreadGroup, 1, 1, 1),
            // 0xFFFFFFFF * 0xFFFFFFFF = 0xFFFFFFFE_00000001: an unsigned product
            // with a high half an imul could not write.
            Instruction(D3D10Opcode.Mov, Destination(OperandType.Temp, 0),
                Immediate(factor1, 0x80000000, 3, 0x00010000)),
            Instruction(D3D10Opcode.Mov, Destination(OperandType.Temp, 1),
                Immediate(factor1, factor1, 5, 0x00010000)),
            Instruction(D3D10Opcode.UMul, Destination(OperandType.Temp, 2),
                Destination(OperandType.Temp, 3), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 1)),
            Instruction(D3D10Opcode.IMul, Destination(OperandType.Temp, 4),
                Destination(OperandType.Temp, 5), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 1)),
            Instruction(D3D10Opcode.UAddC, Destination(OperandType.Temp, 6),
                Destination(OperandType.Temp, 7), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 1)),
            Instruction(D3D10Opcode.UAddC, Destination(OperandType.Temp, 8), Null(),
                Source(OperandType.Temp, 0), Source(OperandType.Temp, 1)),
            Instruction(D3D10Opcode.USubB, Destination(OperandType.Temp, 9),
                Destination(OperandType.Temp, 10), Source(OperandType.Temp, 0),
                Source(OperandType.Temp, 1)),
            // Swap where nonzero: lanes 0, 2 and 3 swap, lane 1 stands still.
            Instruction(D3D10Opcode.Mov, Destination(OperandType.Temp, 11),
                Immediate(0xFFFFFFFF, 0, 1, 0x80000000)),
            Instruction(D3D10Opcode.SwapC, Destination(OperandType.Temp, 12),
                Destination(OperandType.Temp, 13), Source(OperandType.Temp, 11),
                Source(OperandType.Temp, 0), Source(OperandType.Temp, 1)),
            StoreElement(OperandType.Temp, 2, 0),
            StoreElement(OperandType.Temp, 3, 1),
            StoreElement(OperandType.Temp, 4, 2),
            StoreElement(OperandType.Temp, 5, 3),
            StoreElement(OperandType.Temp, 6, 4),
            StoreElement(OperandType.Temp, 7, 5),
            StoreElement(OperandType.Temp, 8, 6),
            StoreElement(OperandType.Temp, 9, 7),
            StoreElement(OperandType.Temp, 10, 8),
            StoreElement(OperandType.Temp, 12, 9),
            StoreElement(OperandType.Temp, 13, 10),
            Instruction(D3D10Opcode.Ret),
        ];
    }

    [Test]
    [Explicit("Writes the carry/borrow/swap shader to %TEMP% for the WARP probe.")]
    public void WritesTheCarryBorrowSwapBytesForWarp()
    {
        string path = Path.Combine(Path.GetTempPath(), "opencode", "dxbc_carry_borrow_swap.fxc");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, AssembleBytes(ShaderType.Compute, 5, 0,
            CarryBorrowSwapInstructions()));
        TestContext.WriteLine(path);
    }

    [Test]
    public void MachineRunsTheCarryTheBorrowAndTheSwap()
    {
        ShaderModel shader = Assemble(ShaderType.Compute, 5, 0, CarryBorrowSwapInstructions());

        Dictionary<string, float[]> results = D3D10Machine.Run(shader, 0);

        // umul: the true 64-bit products of 0xFFFFFFFF^2, 2^31 * -1 as unsigned,
        // 3 * 5 and 2^16 * 2^16.
        AssertLanes(results, 0, 0xFFFFFFFE, 0x7FFFFFFF, 0, 1);
        AssertLanes(results, 1, 1, 0x80000000, 15, 0);
        // imul on the same inputs: signed highs, so the two negative-reading lanes
        // come out zero where umul's did not, and the 2^32 product carries the
        // same high half either way.
        AssertLanes(results, 2, 0, 0, 0, 1);
        AssertLanes(results, 3, 1, 0x80000000, 15, 0);
        // uaddc sums and carries.
        AssertLanes(results, 4, 0xFFFFFFFE, 0x7FFFFFFF, 8, 0x00020000);
        AssertLanes(results, 5, 1, 1, 0, 0);
        // The null-carry form writes the sum only.
        AssertLanes(results, 6, 0xFFFFFFFE, 0x7FFFFFFF, 8, 0x00020000);
        // usubb differences and borrows.
        AssertLanes(results, 7, 0, 0x80000001, 0xFFFFFFFE, 0);
        AssertLanes(results, 8, 0, 1, 1, 0);
        // swapc: lane 1 (zero condition) keeps its pair, the others trade.
        AssertLanes(results, 9, 0xFFFFFFFF, 0x80000000, 5, 0x00010000);
        AssertLanes(results, 10, 0xFFFFFFFF, 0xFFFFFFFF, 3, 0x00010000);
    }

    // A two-destination instruction's sources follow both destinations, so the
    // second destination is not a source and the last source is not skipped.
    [Test]
    public void TheSourcesOfATwoDestinationInstructionAreIntegers()
    {
        ShaderModel shader = Assemble(ShaderType.Compute, 5, 0, [
            Declaration(D3D10Opcode.DclTemps, 11),
            Declaration(D3D10Opcode.DclThreadGroup, 1, 1, 1),
            Instruction(D3D10Opcode.IMul, Null(), Destination(OperandType.Temp, 0, 0x1),
                Component(OperandType.Temp, 1, 0), Component(OperandType.Temp, 2, 0)),
            Instruction(D3D10Opcode.UAddC, Destination(OperandType.Temp, 3, 0x1),
                Destination(OperandType.Temp, 4, 0x1),
                Component(OperandType.Temp, 5, 0), Component(OperandType.Temp, 6, 0)),
            Instruction(D3D10Opcode.Udiv, Destination(OperandType.Temp, 7, 0x1),
                Destination(OperandType.Temp, 8, 0x1),
                Component(OperandType.Temp, 9, 0), Component(OperandType.Temp, 10, 0)),
            Instruction(D3D10Opcode.Ret),
        ]);
        var analysis = new IntegerOperandAnalysis(shader);
        bool IsInteger(uint register) => analysis.IsIntegerRegister(
            new RegisterComponentKey(new D3D10RegisterKey(OperandType.Temp, (int)register), 0));

        Assert.Multiple(() =>
        {
            Assert.That(IsInteger(1), Is.True, "imul's first source");
            Assert.That(IsInteger(2), Is.True, "imul's second source");
            Assert.That(IsInteger(5), Is.True, "uaddc's first source");
            Assert.That(IsInteger(6), Is.True, "uaddc's second source");
            Assert.That(IsInteger(0), Is.True, "imul's low half");
            Assert.That(IsInteger(3), Is.True, "uaddc's sum");
            Assert.That(IsInteger(4), Is.True, "uaddc's carry");
            Assert.That(IsInteger(7), Is.True, "udiv's quotient");
            Assert.That(IsInteger(8), Is.True, "udiv's remainder");
            Assert.That(IsInteger(9), Is.True, "udiv's dividend");
            Assert.That(IsInteger(10), Is.True, "udiv's divisor");
        });
    }

    // A groupshared array is uint when what it stores was made unsigned. The
    // value's maker may be a second destination: udiv's remainder beside its
    // quotient, imul's low half beside its high. Each is first made signed by an
    // ishr, so a search that steps past the real maker finds that instead.
    [Test]
    public void ASecondDestinationIsTheMakerOfWhatItWrites()
    {
        static uint[] Declare(uint array) => Declaration(D3D10Opcode.DclThreadGroupSharedMemoryStructured,
            Register(OperandType.ThreadGroupSharedMemory, array), 4, 1);
        static uint[] MadeSigned(uint register) => Instruction(D3D10Opcode.IShr,
            Destination(OperandType.Temp, register, 0x1), Component(OperandType.Temp, 7, 0), Immediate(1));
        static uint[] MadeUnsigned(uint register) => Instruction(D3D10Opcode.UShr,
            Destination(OperandType.Temp, register, 0x1), Component(OperandType.Temp, 7, 0), Immediate(1));
        static uint[] Store(uint array, uint register) => Instruction(D3D10Opcode.StoreStructured,
            Destination(OperandType.ThreadGroupSharedMemory, array, 0x1), Immediate(0), Immediate(0),
            Component(OperandType.Temp, register, 0));

        ShaderModel shader = Assemble(ShaderType.Compute, 5, 0, [
            Declare(0), Declare(1), Declare(2),
            Declaration(D3D10Opcode.DclTemps, 8),
            Declaration(D3D10Opcode.DclThreadGroup, 1, 1, 1),
            MadeSigned(1), MadeSigned(3), MadeSigned(4),
            MadeUnsigned(5), MadeUnsigned(6),
            // udiv r0.x, r1.x, r5.x, r6.x: the remainder is r1.
            Instruction(D3D10Opcode.Udiv, Destination(OperandType.Temp, 0, 0x1),
                Destination(OperandType.Temp, 1, 0x1),
                Component(OperandType.Temp, 5, 0), Component(OperandType.Temp, 6, 0)),
            // imul r3.x, r4.x, r5.x, r6.x: unsigned factors, so both halves are.
            Instruction(D3D10Opcode.IMul, Destination(OperandType.Temp, 3, 0x1),
                Destination(OperandType.Temp, 4, 0x1),
                Component(OperandType.Temp, 5, 0), Component(OperandType.Temp, 6, 0)),
            Store(0, 1), Store(1, 4), Store(2, 3),
            Instruction(D3D10Opcode.Ret),
        ]);
        var analysis = new IntegerOperandAnalysis(shader);

        Assert.Multiple(() =>
        {
            Assert.That(analysis.IsUnsignedThreadGroupSharedMemory(0), Is.True, "udiv's remainder");
            Assert.That(analysis.IsUnsignedThreadGroupSharedMemory(1), Is.True, "imul's low half");
            Assert.That(analysis.IsUnsignedThreadGroupSharedMemory(2), Is.True, "imul's high half");
        });
    }

    private static uint[] StoreElement(OperandType type, uint register, uint element)
    {
        return Instruction(D3D10Opcode.StoreStructured,
            Destination(OperandType.UnorderedAccessView, 0), Immediate(element),
            Immediate(0), Source(type, register));
    }

    // Structured stores land one dword per key, named by element and byte offset.
    private static void AssertLanes(Dictionary<string, float[]> results, int element,
        params uint[] expected)
    {
        uint[] actual = [.. Enumerable.Range(0, 4).Select(offset =>
            BitConverter.SingleToUInt32Bits(results[$"STORE0[{element}][{4 * offset}]"][0]))];
        Assert.That(actual, Is.EqualTo(expected));
    }

    private static string WriteAsm(ShaderModel shader)
    {
        var stream = new MemoryStream();
        new AsmWriter(shader).Write(stream);
        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }
}
