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
// every call and folds 64-bit arithmetic away). Expected text follows the spec
// pages, which spell the mnemonics and operand forms exactly as fxc's
// disassembler prints them. The machine cases run the hand-assembled shader
// against the interpreter because the equivalence tier can never reach these
// shapes.
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
            swapc r1.xy, r2.zw, r0.xy, r3.xy, r1.xy
            nop
            call l2
            callc_nz r0.x, l2
            label l2
            ret

            """));
    }

    // The listing above follows the spec pages, which are what fxc's disassembler
    // prints. This writes the bytes for `fxc /nologo /dumpbin` to look at; every
    // d3dcompiler entry point (/dumpbin, D3DDisassemble, D3DStripShader) and even
    // the D3D11 runtime's CreateComputeShader refuse a container until it carries
    // a computed DXBC container hash, so the file waits on a hash writer.
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
