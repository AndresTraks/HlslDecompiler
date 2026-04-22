using HlslDecompiler.DirectXShaderModel;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HlslDecompiler.Tests;

public class SwizzleTests
{
    [Test]
    public void TestSwizzle()
    {
        CreateOperand("v0.xy", "yz");

        ShaderModel model = CreateShader([
            new D3D10Instruction(D3D10Opcode.Mov, [..CreateOperand("o0"), ..CreateOperand("v0")], false),
            new D3D10Instruction(D3D10Opcode.Mov, [..CreateOperand("o0.w"), ..CreateOperand("v0.w") ], false),
            new D3D10Instruction(D3D10Opcode.Mov, [..CreateOperand("o0.y"), ..CreateOperand("v0.x") ], false),
            new D3D10Instruction(D3D10Opcode.Mov, [..CreateOperand("o0.xy"), ..CreateOperand("v0.xw") ], false),
            new D3D10Instruction(D3D10Opcode.Mov, [..CreateOperand("o0.yz"), ..CreateOperand("v0.xy", "yz") ], false)
        ]);
        string result = WriteAsm(model);
        string hlsl = WriteHlsl(model);

        Assert.That(result, Is.EqualTo("""
            ps_4_0
            mov o0, v0
            mov o0.w, v0.w
            mov o0.y, v0.x
            mov o0.xy, v0.xw
            mov o0.yz, v0.xy

            """));
    }

    private static ShaderModel CreateShader(IList<Instruction> instructions)
    {
        return new ShaderModel(4, 0, ShaderType.Pixel, instructions);
    }

    private static uint[] CreateOperand(string name, string writeMask = "xyzw")
    {
        uint token = (uint)D3D10OperandNumComponents.Operand4Component;

        var operandType = name[0] == 'o' ? OperandType.Output : OperandType.Input;
        var registerNumber = uint.Parse(name[1].ToString());
        ComponentSelectionMode componentSelectionMode;
        if (operandType == OperandType.Output)
        {
            componentSelectionMode = ComponentSelectionMode.Mask;
            uint mask;
            if (name.Contains('.'))
            {
                mask = 0;
                if (name.Contains('x')) mask |= 1 << 0;
                if (name.Contains('y')) mask |= 1 << 1;
                if (name.Contains('z')) mask |= 1 << 2;
                if (name.Contains('w')) mask |= 1 << 3;
            }
            else
            {
                mask = 0xF;
            }
            token |= mask << 4;
        }
        else if (name.Length == 4)
        {
            componentSelectionMode = ComponentSelectionMode.Select1;
            token |= (uint)"xyzw".IndexOf(name[3]) << 4;
        }
        else
        {
            componentSelectionMode = ComponentSelectionMode.Swizzle;
            uint swizzle;
            if (name.Contains('.'))
            {
                swizzle = ParseSwizzle(name.Substring(name.IndexOf('.') + 1), writeMask);
            }
            else
            {
                swizzle = 0 << 0 | 1 << 2 | 2 << 4 | 3 << 6;
            }
            token |= swizzle << 4;
        }

        uint indexDimension = 1;

        token |= (uint)componentSelectionMode << 2;
        token |= indexDimension << 20;
        token |= (uint)operandType << 12;
        token |= (uint)D3D10OperandIndexRepresentation.Immediate32 << 22;
        return [token, registerNumber];
    }

    private static uint ParseSwizzle(string swizzleString, string writeMask)
    {
        uint swizzle = 0;
        int srcPos = 0;
        for (int comp = 0; comp < 4; comp++)
        {
            if (writeMask.Contains("xyzw"[comp]))
            {
                if (srcPos < swizzleString.Length)
                {
                    uint srcIndex = (uint)"xyzw".IndexOf(swizzleString[srcPos]);
                    swizzle |= srcIndex << (comp * 2);
                    srcPos++;
                }
            }
        }
        return swizzle;
    }

    private static string WriteAsm(ShaderModel model)
    {
        var asmWriter = new AsmWriter(model);
        var stream = new MemoryStream();
        asmWriter.Write(stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string WriteHlsl(ShaderModel model)
    {
        var stream = new MemoryStream();
        new HlslSimpleWriter(model).Write(new StreamWriter(stream));
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
