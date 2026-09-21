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
        // A compiled statement can be two lines - a declaration and the call that
        // fills it - and each wants the indent.
        foreach (string line in value.Split("\r\n"))
        {
            internalWriter.Write(indent);
            internalWriter.WriteLine(line);
        }
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
        // HLSL output always uses Windows line endings, regardless of host OS,
        // so decompiled shaders and their fxc round-trip are reproducible everywhere.
        writer.NewLine = "\r\n";
        internalWriter = writer;
        WriteInternal();
    }

    private void WriteInternal()
    {
        _ast = InstructionParser.Parse(_shader);
        _registers = _ast.RegisterState;

        WriteConstantDeclarations();
        WriteThreadGroupSharedMemoryDeclarations();

        // The geometry shader signature always names the input struct, so it has to
        // be declared even when it holds a single register.
        if (_registers.MethodInputRegisters.Count > 1
            || _shader.Type == ShaderType.Geometry
            || _shader.Type == ShaderType.Domain)
        {
            WriteInputStructureDeclaration();
        }

        if (_registers.TessellatorDomain != D3D10TessellatorDomain.Undefined)
        {
            WritePatchConstantStructureDeclaration();
        }

        if (HasOutputStruct)
        {
            WriteOutputStructureDeclaration();
        }

        if (_registers.MaxOutputVertexCount != null)
        {
            WriteLine("[maxvertexcount({0})]", _registers.MaxOutputVertexCount);
        }

        // A geometry shader run more than once per primitive, each run knowing
        // which it is.
        if (_registers.GSInstanceCount != null)
        {
            WriteLine("[instance({0})]", _registers.GSInstanceCount);
        }

        if (_registers.TessellatorDomain != D3D10TessellatorDomain.Undefined)
        {
            WriteLine("[domain(\"{0}\")]", _registers.TessellatorDomain switch
            {
                D3D10TessellatorDomain.Isoline => "isoline",
                D3D10TessellatorDomain.Triangle => "tri",
                _ => "quad",
            });
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
            int written = 0;
            foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
            {
                // A texture buffer's variables keep their block, below, and so do
                // a named constant buffer's.
                if (declaration is D3D10ConstantDeclaration { IsTextureBuffer: true }
                    || IsNamedBuffer(declaration))
                {
                    continue;
                }
                if (declaredNames.Add(declaration.Name))
                {
                    WriteLine(compiler.Compile(declaration));
                    written++;
                }
            }
            if (written != 0)
            {
                WriteLine();
            }

            // The globals are flattened - they bind to the one register the
            // compiler gives them either way - but a buffer the shader named binds
            // where it was declared, and with two of them which is which is the
            // difference between the world matrix and the view projection. So a
            // named buffer keeps its block and its slot.
            foreach (var buffer in _registers.ConstantDeclarations
                .OfType<D3D10ConstantDeclaration>()
                .Where(IsNamedBuffer)
                .GroupBy(d => d.BufferName))
            {
                WriteLine($"cbuffer {buffer.Key} : register(b{buffer.First().RegisterIndex})");
                WriteLine("{");
                indent = "\t";
                foreach (D3D10ConstantDeclaration member in buffer)
                {
                    WriteLine(compiler.Compile(member));
                }
                indent = "";
                WriteLine("};");
                WriteLine();
            }

            // A cbuffer is flattened into globals - they bind to the same register
            // and are read the same way - but a texture buffer binds to a t register
            // and is read with ld, so the block and its name are what make the
            // shader the one it was.
            foreach (var textureBuffer in _registers.ConstantDeclarations
                .OfType<D3D10ConstantDeclaration>()
                .Where(d => d.IsTextureBuffer)
                .GroupBy(d => d.BufferName))
            {
                WriteLine($"tbuffer {textureBuffer.Key}");
                WriteLine("{");
                indent = "\t";
                foreach (D3D10ConstantDeclaration member in textureBuffer)
                {
                    WriteLine(compiler.Compile(member));
                }
                indent = "";
                WriteLine("};");
                WriteLine();
            }
        }

        // Emitted after the uniforms so that a subscript reading one is already in
        // scope, though nothing in a literal array can reference anything anyway.
        if (_registers.ImmediateConstantBuffer.Count != 0)
        {
            WriteLine("static const float4 icb[{0}] =", _registers.ImmediateConstantBuffer.Count);
            WriteLine("{");
            indent = "	";
            foreach (ConstantRegister row in _registers.ImmediateConstantBuffer)
            {
                string components = string.Join(", ", row.Value.Select(
                    v => v.ToString(CultureInfo.InvariantCulture)));
                WriteLine($"float4({components}),");
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

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
            // A structured buffer whose element is a struct needs that struct named
            // before the buffer that holds it. The reflection data has the members
            // but no name for the type, so one is made from the buffer's.
            var elementCompiler = new ConstantDeclarationCompiler();
            foreach (var resource in _registers.ResourceDefinitions
                .Where(r => r.ElementType?.MemberInfo != null && r.ElementType.MemberInfo.Count != 0))
            {
                WriteLine($"struct {GetStructuredElementTypeName(resource)}");
                WriteLine("{");
                indent = "	";
                foreach (ShaderStructMemberInfo member in resource.ElementType.MemberInfo)
                {
                    WriteLine(elementCompiler.Compile(member));
                }
                indent = "";
                WriteLine("};");
                WriteLine();
            }

            int declared = 0;
            foreach (var resource in _registers.ResourceDefinitions)
            {
                // A texture buffer is declared by its block, with the constants,
                // and not as a resource of its own.
                if (resource.ShaderInputType == D3DShaderInputType.TBuffer)
                {
                    continue;
                }
                if (resource.ShaderInputType == D3DShaderInputType.Texture)
                {
                    WriteLine($"{resource.TypeName} {resource.Name};");
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
                else if (resource.ShaderInputType == D3DShaderInputType.ByteAddress)
                {
                    WriteLine($"ByteAddressBuffer {resource.Name} : register(t{resource.BindPoint});");
                }
                // An append or consume buffer declares itself the same way a
                // structured one does and is bound with a counter beside it, which
                // is what the reflection data calls it and the only place it is
                // said: the bytecode declares dcl_uav_structured for all three.
                else if (resource.ShaderInputType == D3DShaderInputType.UavAppendStructured)
                {
                    WriteLine($"AppendStructuredBuffer<{GetStructuredElementType(resource)}> {resource.Name} : register(u{resource.BindPoint});");
                }
                else if (resource.ShaderInputType == D3DShaderInputType.UavConsumeStructured)
                {
                    WriteLine($"ConsumeStructuredBuffer<{GetStructuredElementType(resource)}> {resource.Name} : register(u{resource.BindPoint});");
                }
                else if (resource.ShaderInputType == D3DShaderInputType.UavRWStucturedWithCounter)
                {
                    WriteLine($"RWStructuredBuffer<{GetStructuredElementType(resource)}> {resource.Name} : register(u{resource.BindPoint});");
                }
                else if (resource.ShaderInputType == D3DShaderInputType.UavRWByteAddress)
                {
                    WriteLine($"RWByteAddressBuffer {resource.Name} : register(u{resource.BindPoint});");
                }
                // A typed unordered access view is the texture type it would be as a
                // resource, written RW - and it always names its element type, where
                // a read only texture names one only when it holds integers: there is
                // no bare RWTexture2D that means RWTexture2D<float4>.
                else if (resource.ShaderInputType == D3DShaderInputType.UavRWTyped)
                {
                    WriteLine($"{resource.ReadWriteTypeName} {resource.Name} : register(u{resource.BindPoint});");
                }
                else
                {
                    throw new NotImplementedException();
                }
                declared++;
            }
            if (declared != 0)
            {
                WriteLine();
            }
        }
    }

    /// <summary>
    /// Declares a compute shader's groupshared arrays, g0[64] and so on, at file
    /// scope where HLSL wants them. The stride gives the element width; the type is
    /// float unless every store into the array is an integer, the same rule as an
    /// indexable temp. Nothing in the bytecode names them, so they keep the register.
    /// </summary>
    // A constant buffer the shader declared and named, as against the $Globals
    // fxc gathers the loose variables into.
    private static bool IsNamedBuffer(ConstantDeclaration declaration)
    {
        return declaration is D3D10ConstantDeclaration { IsTextureBuffer: false, BufferName: not null and not "$Globals" };
    }

    private void WriteThreadGroupSharedMemoryDeclarations()
    {
        if (_registers.ThreadGroupSharedMemory.Count == 0)
        {
            return;
        }
        var integerOperandAnalysis = new IntegerOperandAnalysis(_shader);
        foreach ((int register, (int stride, int elements)) in _registers.ThreadGroupSharedMemory.OrderBy(t => t.Key))
        {
            int components = stride / sizeof(float);
            if (components < 1 || components > 4 || stride % sizeof(float) != 0)
            {
                throw new NotImplementedException($"groupshared element stride {stride}");
            }
            string type = integerOperandAnalysis.IsIntegerThreadGroupSharedMemory(register) ? "int" : "float";
            string size = components == 1 ? "" : components.ToString(CultureInfo.InvariantCulture);
            WriteLine($"groupshared {type}{size} g{register}[{elements}];");
        }
        WriteLine();
    }

    /// <summary>
    /// What one element holds. The reflection data says outright where it is there;
    /// failing that the width comes from the declaration stride and the type is
    /// assumed float, which is what a StructuredBuffer usually holds.
    /// </summary>
    /// <summary>
    /// Declares the shader's indexable temps, x0[4] and so on, as local arrays. An
    /// element is a float4 unless every write to the array is an integer
    /// instruction - an array that carries both has to stay float, the same as a
    /// register that does.
    /// </summary>
    protected void WriteIndexableTempDeclarations(IntegerOperandAnalysis integerOperandAnalysis)
    {
        foreach ((int register, (int elements, int components)) in _registers.IndexableTemps.OrderBy(t => t.Key))
        {
            bool isInteger = integerOperandAnalysis?.IsIntegerIndexableTemp(register) == true;
            string type = isInteger ? "int" : "float";
            string size = components == 1 ? "" : components.ToString(CultureInfo.InvariantCulture);
            WriteLine($"{type}{size} x{register}[{elements}];");
        }
    }

    /// <summary>The name given to a structured buffer's struct element. The
    /// reflection data names the members and not the type, so the buffer names
    /// it.</summary>
    private static string GetStructuredElementTypeName(ResourceDefinition resource)
    {
        return char.ToUpperInvariant(resource.Name[0]) + resource.Name[1..] + "Element";
    }

    protected string GetStructuredElementType(ResourceDefinition resource)
    {
        if (resource.ElementType?.MemberInfo != null && resource.ElementType.MemberInfo.Count != 0)
        {
            return GetStructuredElementTypeName(resource);
        }
        if (resource.ElementType != null)
        {
            string scalar = resource.ElementType.ParameterType.ToString().ToLower();
            int width = resource.ElementType.Columns;
            // A buffer of transforms holds matrices, and a matrix has rows as well
            // as columns. Taking the columns alone made a StructuredBuffer<float4x4>
            // of instance transforms into one of float4, where the stride says the
            // element is four times that.
            if (resource.ElementType.Rows > 1)
            {
                return $"{scalar}{resource.ElementType.Rows}x{width}";
            }
            return width > 1 ? scalar + width : scalar;
        }

        int components = _registers.GetStructuredBufferComponents(
            resource.ShaderInputType, resource.BindPoint);
        return components > 1 ? "float" + components : "float";
    }

    // A compute shader takes its thread and group indices the way any other shader
    // takes its inputs, and more than one of them needs a structure to hold them.
    private string GetInputStructureName()
    {
        return _shader.Type switch
        {
            ShaderType.Pixel => "PS_IN",
            ShaderType.Vertex => "VS_IN",
            ShaderType.Geometry => "GS_IN",
            ShaderType.Compute => "CS_IN",
            ShaderType.Domain => "DS_IN",
            _ => throw new NotImplementedException(_shader.Type.ToString()),
        };
    }

    private void WriteInputStructureDeclaration()
    {
        string inputStructType = GetInputStructureName();
        WriteLine($"struct {inputStructType}");
        WriteLine("{");
        indent = "\t";
        ICollection<RegisterDeclaration> inputs = _registers.MethodInputRegisters;
        // A domain shader reads a patch, which is an array of vertices the way a
        // geometry shader's input is: one declaration per register, not one per
        // control point. Its struct is the control point, so the domain location -
        // which is per run rather than per point - is not a field of it.
        if (_shader.Type == ShaderType.Domain)
        {
            inputs = [.. inputs
                .Where(r => r.RegisterKey is D3D10RegisterKey
                    { OperandType: not OperandType.InputDomainPoint })
                .GroupBy(r => (r.RegisterKey as D3D10RegisterKey).GetGSBaseKey())
                .Select(g => g.First())];
        }
        if (_shader.Type == ShaderType.Geometry)
        {
            // One member per register across the vertices - and per semantic
            // within a register, where two are packed into one.
            inputs = inputs
                .GroupBy(r => ((r.RegisterKey as D3D10RegisterKey).GetGSBaseKey(), r.Semantic))
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

    /// <summary>
    /// What the hull shader computed once for the patch. The signature chunk holds
    /// all of it, read or not, which is what is wanted: fxc refuses a domain shader
    /// whose signature leaves the tessellation factors out even when nothing touches
    /// them. Older bytecode without the chunk falls back to the factors the domain
    /// implies, which is the least that will compile.
    /// </summary>
    private void WritePatchConstantStructureDeclaration()
    {
        WriteLine($"struct {PatchConstants.StructureName}");
        WriteLine("{");
        indent = "\t";
        IList<RegisterSignature> signatures = _shader.PatchConstantSignatures;
        if (signatures.Count == 0)
        {
            foreach (string field in ImpliedTessellationFactors())
            {
                WriteLine(field);
            }
        }
        else
        {
            foreach (string field in PatchConstants.Fields(signatures))
            {
                WriteLine(field);
            }
        }
        indent = "";
        WriteLine("};");
        WriteLine();
    }

    private IEnumerable<string> ImpliedTessellationFactors()
    {
        switch (_registers.TessellatorDomain)
        {
            case D3D10TessellatorDomain.Isoline:
                yield return "float edges[2] : SV_TessFactor;";
                break;
            case D3D10TessellatorDomain.Triangle:
                yield return "float edges[3] : SV_TessFactor;";
                yield return "float inside : SV_InsideTessFactor;";
                break;
            default:
                yield return "float edges[4] : SV_TessFactor;";
                yield return "float inside[2] : SV_InsideTessFactor;";
                break;
        }
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
        // Shader model 3 declares its outputs, so the order they were found in is the
        // order the shader wrote them down. Before that the output registers are fixed
        // ones - oPos, oT0, oFog - and are never declared, so a field's place in the
        // struct would otherwise be whichever of them fxc happened to write to first.
        if (_shader.MajorVersion <= 2)
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

    /// <summary>
    /// Whether the outputs are written through a struct rather than returned as the
    /// one expression. A geometry shader always is: it writes its vertices through
    /// the stream, so the body says `o.member` however few members there are.
    /// </summary>
    protected bool HasOutputStruct =>
        _registers.MethodOutputRegisters.Count > 1 || _shader.Type == ShaderType.Geometry;

    private string GetMethodSemantic()
    {
        // `void main(...) : SV_Position` is an error - X3076, a void function cannot
        // have a semantic - and a geometry or compute shader returns void whatever
        // its one output register might have suggested.
        if (GetMethodReturnType() != "void" && _registers.MethodOutputRegisters.Count == 1)
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
            string stream = GetStreamType(_registers.PrimitiveTopology);
            string primitiveId = _registers.PrimitiveIdDeclaration == null
                ? ""
                : $"{CompileRegisterDeclaration(_registers.PrimitiveIdDeclaration)}, ";
            string instanceId = _registers.GSInstanceIdDeclaration == null
                ? ""
                : $"{CompileRegisterDeclaration(_registers.GSInstanceIdDeclaration)}, ";
            return $"{primitive} GS_IN i[{vertexCount}], {primitiveId}{instanceId}"
                + $"inout {stream}<GS_OUT> stream";
        }
        if (_shader.Type == ShaderType.Domain)
        {
            // The patch is an array of control points and the domain location says
            // where in it this run is. Everything else the patch carries - the
            // tessellation factors the hull shader computed - is read through the
            // constant parameter, which this shader does not touch unless it says
            // so.
            RegisterDeclaration location = _registers.MethodInputRegisters
                .FirstOrDefault(r => r.RegisterKey is D3D10RegisterKey
                    { OperandType: OperandType.InputDomainPoint });
            string domainLocation = location == null
                ? ""
                : CompileRegisterDeclaration(location) + ", ";
            return $"{PatchConstants.StructureName} {PatchConstants.ParameterName}, {domainLocation}"
                + $"const OutputPatch<{GetInputStructureName()}, "
                + $"{_registers.InputControlPointCount}> patch";
        }
        if (_registers.MethodInputRegisters.Count == 0)
        {
            return string.Empty;
        }
        else if (_registers.MethodInputRegisters.Count == 1)
        {
            var input = _registers.MethodInputRegisters.First();
            return CompileRegisterDeclaration(input);
        }

        return GetInputStructureName() + " i";
    }

    // A geometry shader can only emit strips, so the declared topology names the
    // stream type outright.
    private static string GetStreamType(D3D10PrimitiveTopology? topology)
    {
        return topology switch
        {
            D3D10PrimitiveTopology.PointList => "PointStream",
            D3D10PrimitiveTopology.LineStrip => "LineStream",
            D3D10PrimitiveTopology.TriangleStrip => "TriangleStream",
            _ => throw new NotImplementedException(topology?.ToString() ?? "no output topology"),
        };
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
        string subscript = input.ArrayLength > 1 ? $"[{input.ArrayLength}]" : "";
        return $"{input.TypeName} {input.Name}{subscript} : {input.Semantic}";
    }
}