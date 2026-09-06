using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;

namespace HlslDecompiler;

public abstract class HlslWriter
{
    protected readonly ShaderModel _shader;

    TextWriter internalWriter;
    protected string indent = "";

    protected HlslAst _ast;
    protected RegisterState _registers;

    public HlslWriter(ShaderModel shader)
    {
        _shader = shader;
    }

    protected abstract void WriteMethodBody();

    protected void WriteLine()
    {
        internalWriter.WriteLine();
    }

    protected void WriteLine(string value)
    {
        internalWriter.Write(indent);
        internalWriter.WriteLine(value);
    }

    protected void WriteLine(string format, params object[] args)
    {
        internalWriter.Write(indent);
        internalWriter.WriteLine(format, args);
    }

    public void Write(string hlslFilename)
    {
        using var file = new FileStream(hlslFilename, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(file);
        Write(writer);
    }

    public void Write(TextWriter writer)
    {
        internalWriter = writer;
        WriteInternal();
    }

    private void WriteInternal()
    {
        _ast = InstructionParser.Parse(_shader);
        _registers = _ast.RegisterState;

        WriteConstantDeclarations();

        // The geometry shader signature always names the input struct, so it has to
        // be declared even when it holds a single register.
        if (_registers.MethodInputRegisters.Count > 1
            || _shader.Type == ShaderType.Geometry)
        {
            WriteInputStructureDeclaration();
        }

        if (_registers.MethodOutputRegisters.Count > 1)
        {
            WriteOutputStructureDeclaration();
        }

        if (_registers.MaxOutputVertexCount != null)
        {
            WriteLine("[maxvertexcount({0})]", _registers.MaxOutputVertexCount);
        }

        if (_registers.NumThreads != null)
        {
            WriteLine($"[numthreads({_registers.NumThreads[0]}, {_registers.NumThreads[1]}, {_registers.NumThreads[2]})]", _registers.MaxOutputVertexCount);
        }

        string methodReturnType = GetMethodReturnType();
        string methodParameters = GetMethodParameters();
        string methodSemantic = GetMethodSemantic();

        WriteLine("{0} main({1}){2}", methodReturnType, methodParameters, methodSemantic);
        WriteLine("{");
        indent = "\t";

        WriteMethodBody();

        indent = "";
        WriteLine("}");
    }

    private void WriteConstantDeclarations()
    {
        if (_registers.ConstantDeclarations.Count != 0)
        {
            var compiler = new ConstantDeclarationCompiler();

            foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
            {
                compiler.SetStructOrder(declaration);
            }

            IList<ShaderTypeInfo> structs = compiler.GetOrderedStructs();
            for (int i = 0; i < structs.Count; i++)
            {
                WriteLine($"struct struct{i + 1}");
                WriteLine("{");
                indent = "\t";
                foreach (var member in structs[i].MemberInfo)
                {
                    WriteLine(compiler.Compile(member));
                }
                indent = "";
                WriteLine("};");
                WriteLine();
            }

            // One variable can occupy a register in more than one set - an int used
            // both as a loop bound and in float arithmetic lands in i# and c# alike -
            // and the constant table lists it once per register. It is still one
            // declaration.
            var declaredNames = new HashSet<string>();
            foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
            {
                if (declaredNames.Add(declaration.Name))
                {
                    WriteLine(compiler.Compile(declaration));
                }
            }
            WriteLine();
        }

        // Emitted after the uniforms so that a subscript reading one is already in
        // scope, though nothing in a literal array can reference anything anyway.
        foreach (ConstantArray constantArray in _registers.ConstantArrays)
        {
            WriteLine("static const float4 {0}[{1}] =", constantArray.Name, constantArray.Registers.Count);
            WriteLine("{");
            indent = "	";
            foreach (ConstantRegister register in constantArray.Registers)
            {
                string components = string.Join(", ", register.Value.Select(
                    v => v.ToString(CultureInfo.InvariantCulture)));
                WriteLine($"float4({components}),");
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        if (_registers.ResourceDefinitions != null && _registers.ResourceDefinitions.Count != 0)
        {
            foreach (var resource in _registers.ResourceDefinitions)
            {
                if (resource.ShaderInputType == D3DShaderInputType.Texture)
                {
                    WriteLine($"{resource.Dimension} {resource.Name};");
                }
                else if (resource.ShaderInputType == D3DShaderInputType.Sampler)
                {
                    // SampleCmp and SampleCmpLevelZero only take the comparison kind,
                    // which the reflection data flags.
                    string samplerType = resource.Flags.HasFlag(D3DShaderInputFlags.ComparisonSampler)
                        ? "SamplerComparisonState"
                        : "SamplerState";
                    WriteLine($"{samplerType} {resource.Name};");
                }
                else if (resource.ShaderInputType == D3DShaderInputType.Structured)
                {
                    WriteLine($"StructuredBuffer<{GetStructuredElementType(resource)}> {resource.Name} : register(t{resource.BindPoint});");
                }
                else if (resource.ShaderInputType == D3DShaderInputType.UavRWStructured)
                {
                    WriteLine($"RWStructuredBuffer<{GetStructuredElementType(resource)}> {resource.Name} : register(u{resource.BindPoint});");
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            WriteLine();
        }
    }

    // The element width comes from the declaration stride, so a
    // StructuredBuffer<float4> is no longer declared as one of float.
    private string GetStructuredElementType(ResourceDefinition resource)
    {
        int components = _registers.GetStructuredBufferComponents(
            resource.ShaderInputType, resource.BindPoint);
        return components > 1 ? "float" + components : "float";
    }

    private void WriteInputStructureDeclaration()
    {
        string inputStructType = _shader.Type switch
        {
            ShaderType.Pixel => "PS_IN",
            ShaderType.Vertex => "VS_IN",
            ShaderType.Geometry => "GS_IN",
            _ => throw new NotImplementedException(_shader.Type.ToString()),
        };
        WriteLine($"struct {inputStructType}");
        WriteLine("{");
        indent = "\t";
        ICollection<RegisterDeclaration> inputs = _registers.MethodInputRegisters.Values;
        if (_shader.Type == ShaderType.Geometry)
        {
            inputs = inputs
                .GroupBy(r => (r.RegisterKey as D3D10RegisterKey).GetGSBaseKey())
                .Select(g => g.First())
                .ToList();
        }
        foreach (var input in inputs)
        {
            WriteLine(CompileRegisterDeclaration(input) + ';');
        }
        indent = "";
        WriteLine("};");
        WriteLine();
    }

    private void WriteOutputStructureDeclaration()
    {
        string outputStructType;
        if (_shader.Type == ShaderType.Pixel)
        {
            outputStructType = "PS_OUT";
        }
        else if (_shader.Type == ShaderType.Vertex)
        {
            outputStructType = "VS_OUT";
        }
        else if (_shader.Type == ShaderType.Geometry)
        {
            outputStructType = "GS_OUT";
        }
        else
        {
            return;
        }

        WriteLine($"struct {outputStructType}");
        WriteLine("{");
        indent = "\t";
        IList<RegisterDeclaration> outputs = _registers.MethodOutputRegisters;
        if (_shader.MajorVersion == 1)
        {
            outputs = outputs.OrderBy(o => o.Semantic).ToList();
        }
        foreach (var output in outputs)
        {
            WriteLine(CompileRegisterDeclaration(output) + ';');
        }
        indent = "";
        WriteLine("};");
        WriteLine();
    }

    protected string GetMethodReturnType()
    {
        if (_shader.Type == ShaderType.Geometry)
        {
            return "void";
        }
        return _registers.MethodOutputRegisters.Count switch
        {
            0 => "void",
            1 => _registers.MethodOutputRegisters.First().TypeName,
            _ => _shader.Type == ShaderType.Pixel ? "PS_OUT" : "VS_OUT",
        };
    }

    private string GetMethodSemantic()
    {
        if (_registers.MethodOutputRegisters.Count == 1)
        {
            string semantic = _registers.MethodOutputRegisters.First().Semantic;
            return $" : {semantic}";
        }
        return string.Empty;
    }

    private string GetMethodParameters()
    {
        if (_shader.Type == ShaderType.Geometry)
        {
            string primitive = _registers.InputPrimitive.Value.ToHlslString();
            int vertexCount = GetPrimitiveVertexCount(_registers.InputPrimitive.Value);
            return $"{primitive} GS_IN i[{vertexCount}], inout TriangleStream<GS_OUT> stream";
        }
        if (_registers.MethodInputRegisters.Count == 0)
        {
            return string.Empty;
        }
        else if (_registers.MethodInputRegisters.Count == 1)
        {
            var input = _registers.MethodInputRegisters.Values.First();
            return CompileRegisterDeclaration(input);
        }

        return _shader.Type == ShaderType.Pixel
                ? "PS_IN i"
                : "VS_IN i";
    }

    private static int GetPrimitiveVertexCount(D3D10Primitive primitive)
    {
        return primitive switch
        {
            D3D10Primitive.Point => 1,
            D3D10Primitive.Line => 2,
            D3D10Primitive.Triangle => 3,
            D3D10Primitive.LineAdj => 4,
            D3D10Primitive.TriangleAdj => 6,
            _ => throw new NotImplementedException(primitive.ToString()),
        };
    }

    private static string CompileRegisterDeclaration(RegisterDeclaration input)
    {
        return $"{input.TypeName} {input.Name} : {input.Semantic}";
    }
}