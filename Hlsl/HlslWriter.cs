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

    /// <summary>
    /// Which of a hull shader's two functions is being written. A hull shader is the
    /// one shader whose bytecode is more than one program, and the two differ in what
    /// they return and in what their output registers mean, so which one is in hand
    /// has to be part of the writer's state rather than read off the shader.
    /// </summary>
    protected enum HullFunction
    {
        None,
        ControlPoint,
        PatchConstant,
    }

    protected HullFunction _hullFunction = HullFunction.None;

    /// <summary>
    /// The bytecode being written: the whole shader, or one phase of a hull shader
    /// with the declarations it shares. The instruction writer works through this
    /// rather than through the shader, so that a phase writes only its own body.
    /// </summary>
    protected ShaderModel _phaseShader;

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
        if (_shader.Type == ShaderType.Hull)
        {
            WriteHullShader();
            return;
        }

        _phaseShader = _shader;
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

        WriteFunction($"{methodReturnType} main({methodParameters}){methodSemantic}",
            WriteMethodBody);
    }

    /// <summary>
    /// A hull shader, which HLSL writes as two functions: the one that runs per
    /// output control point, and the one that computes the constants of the whole
    /// patch. The bytecode runs them as phases under one name, so the declarations
    /// they share are written once, from the phase that has the whole patch in view,
    /// and each function is then written from its own phase's parse.
    /// </summary>
    private void WriteHullShader()
    {
        HullShaderAst hull = InstructionParser.ParseHullShader(_shader);

        // The control point phase declares the whole of the patch it reads and the
        // whole of the point it writes; the patch constant phases declare only the
        // control point registers they happen to touch. So the structs come from it,
        // where there is one - and so do the declarations both phases share, which
        // either has in full.
        EnterPhase(HullFunction.ControlPoint, hull.ControlPoint ?? hull.PatchConstant);
        WriteConstantDeclarations();
        WriteThreadGroupSharedMemoryDeclarations();
        if (hull.ControlPoint != null)
        {
            WriteInputStructureDeclaration();
            WriteOutputStructureDeclaration();
        }
        else
        {
            // Nothing declared the patch, so the signatures are all there is to go
            // on. They say the whole of it, which is what the missing phase means.
            WriteSignatureStructure(GetInputStructureName(), _shader.InputSignatures);
            WriteSignatureStructure(GetOutputStructureName(), _shader.OutputSignatures);
        }
        WritePatchConstantStructureDeclaration();

        if (hull.PatchConstant != null)
        {
            EnterPhase(HullFunction.PatchConstant, hull.PatchConstant);
            WriteFunction($"{GetMethodReturnType()} {PatchConstants.FunctionName}"
                + $"({GetMethodParameters()})", WriteMethodBody);
            WriteLine();
        }

        if (hull.ControlPoint != null)
        {
            EnterPhase(HullFunction.ControlPoint, hull.ControlPoint);
            WriteTessellatorAttributes();
            WriteFunction(
                $"{GetMethodReturnType()} main({GetMethodParameters()}){GetMethodSemantic()}",
                WriteMethodBody);
            return;
        }

        _hullFunction = HullFunction.ControlPoint;
        WriteTessellatorAttributes();
        string controlPointId = CompileRegisterDeclaration(ControlPointIdDeclaration());
        WriteFunction(
            $"{GetOutputStructureName()} main(InputPatch<{GetInputStructureName()}, "
                + $"{_registers.InputControlPointCount}> patch, {controlPointId})",
            WritePassThroughControlPoint);
    }

    /// <summary>
    /// A control point phase fxc left out, because the shader's was a copy: every
    /// point came out as it went in. Nothing in the bytecode says so - there is no
    /// phase to read - so it is the two signatures agreeing that says it, and the
    /// copy is written back out a field at a time.
    /// </summary>
    private void WritePassThroughControlPoint()
    {
        WriteLine($"{GetOutputStructureName()} o;");
        WriteLine();
        RegisterDeclaration id = ControlPointIdDeclaration();
        foreach (RegisterSignature output in _shader.OutputSignatures)
        {
            RegisterSignature input = _shader.InputSignatures.FirstOrDefault(
                i => i.Name == output.Name && i.Index == output.Index);
            if (input == null)
            {
                throw new NotImplementedException(
                    $"A dropped control point phase whose output {output} was not an input");
            }
            string field = FromSignature(output).Name;
            WriteLine($"o.{field} = patch[{id.Name}].{FromSignature(input).Name};");
        }
        WriteLine();
        WriteLine("return o;");
    }

    /// <summary>
    /// Which control point this run computes. Where a phase declares it the
    /// declaration comes from the dcl; where there is no phase it is still a
    /// parameter of main, and this is what it would have said.
    /// </summary>
    private static RegisterDeclaration ControlPointIdDeclaration()
    {
        const int UInt32ComponentType = 1;
        return new RegisterDeclaration(
            new D3D10RegisterKey(OperandType.OutputControlPointID, 0),
            "SV_OutputControlPointID",
            1)
        {
            ComponentType = UInt32ComponentType,
        };
    }

    /// <summary>
    /// A struct written from a signature chunk rather than from what the shader
    /// declared. The chunk names every element and says how wide it is, which is all
    /// a field needs, and it is there whether any phase read the register or not.
    /// </summary>
    private void WriteSignatureStructure(string name, IList<RegisterSignature> signatures)
    {
        WriteLine($"struct {name}");
        WriteLine("{");
        indent = "\t";
        foreach (RegisterSignature signature in signatures)
        {
            WriteLine(CompileRegisterDeclaration(FromSignature(signature)) + ';');
        }
        indent = "";
        WriteLine("};");
        WriteLine();
    }

    private static RegisterDeclaration FromSignature(RegisterSignature signature)
    {
        string semantic = signature.Index == 0
            ? signature.Name
            : signature.Name + signature.Index;
        return new RegisterDeclaration(signature.RegisterKey, semantic, signature.Mask)
        {
            ComponentType = signature.ComponentType,
        };
    }

    /// <summary>The signature line, the braces, and the body between them.</summary>
    private void WriteFunction(string signature, Action writeBody)
    {
        WriteLine(signature);
        WriteLine("{");
        indent = "\t";
        writeBody();
        indent = "";
        WriteLine("}");
    }

    private void EnterPhase(HullFunction function, HullPhase phase)
    {
        _hullFunction = function;
        _phaseShader = phase.Shader;
        _ast = phase.Ast;
        _registers = phase.Ast.RegisterState;
    }

    /// <summary>
    /// What the tessellator is to do, which only a hull shader says: what it divides,
    /// how it cuts an edge, what it makes of the result, how many points come out,
    /// and which function computes the factors. [maxtessfactor] is not among them.
    /// fxc emits dcl_hs_max_tessfactor only where it inserted the clamp to that bound
    /// itself, and a decompilation already carries the clamp as a min, so the
    /// declaration does not come back whether the attribute is written or not -
    /// measured both ways. What the shader computes is the same either way; the
    /// driver loses a hint about how far it will be asked to subdivide.
    /// </summary>
    private void WriteTessellatorAttributes()
    {
        WriteLine("[domain(\"{0}\")]", _registers.TessellatorDomain switch
        {
            D3D10TessellatorDomain.Isoline => "isoline",
            D3D10TessellatorDomain.Triangle => "tri",
            _ => "quad",
        });
        if (_registers.TessellatorPartitioning != D3D10TessellatorPartitioning.Undefined)
        {
            WriteLine("[partitioning(\"{0}\")]", _registers.TessellatorPartitioning switch
            {
                D3D10TessellatorPartitioning.Integer => "integer",
                D3D10TessellatorPartitioning.Pow2 => "pow2",
                D3D10TessellatorPartitioning.FractionalOdd => "fractional_odd",
                _ => "fractional_even",
            });
        }
        if (_registers.TessellatorOutputPrimitive != D3D10TessellatorOutputPrimitive.Undefined)
        {
            WriteLine("[outputtopology(\"{0}\")]", _registers.TessellatorOutputPrimitive switch
            {
                D3D10TessellatorOutputPrimitive.Point => "point",
                D3D10TessellatorOutputPrimitive.Line => "line",
                D3D10TessellatorOutputPrimitive.TriangleClockwise => "triangle_cw",
                _ => "triangle_ccw",
            });
        }
        WriteLine("[outputcontrolpoints({0})]", _registers.OutputControlPointCount);
        WriteLine("[patchconstantfunc(\"{0}\")]", PatchConstants.FunctionName);
    }

    // The resources whose element is a struct of its own, in declaration order.
    private IEnumerable<ResourceDefinition> StructuredElementTypes()
    {
        if (_registers.ResourceDefinitions == null)
        {
            return [];
        }
        return _registers.ResourceDefinitions
            .Where(r => r.ElementType?.MemberInfo != null && r.ElementType.MemberInfo.Count != 0);
    }

    private void WriteConstantDeclarations()
    {
        // One compiler for the uniforms and for the structured buffer elements
        // alike: a struct-typed member is declared once, as struct1, struct2 and
        // so on, and whichever of the two holds it names that one declaration.
        // With a compiler each, both numbered from 1, a shader with a struct on
        // both sides had two different structs both called struct1 - and one
        // with a struct only in a buffer element declared it nowhere at all.
        var compiler = new ConstantDeclarationCompiler();
        foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
        {
            compiler.SetStructOrder(declaration);
        }
        foreach (ResourceDefinition resource in StructuredElementTypes())
        {
            compiler.SetMemberStructOrder(resource.ElementType);
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

        if (_registers.ConstantDeclarations.Count != 0)
        {
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
                bool saysWhereItGoes = NeedsPackOffset(buffer);
                foreach (D3D10ConstantDeclaration member in buffer)
                {
                    string declaration = compiler.Compile(member);
                    if (saysWhereItGoes)
                    {
                        declaration = declaration.TrimEnd(';')
                            + $" : {PackOffset(member.VariableOffset)};";
                    }
                    WriteLine(declaration);
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
            // One declaration per array in it. Written as one array of every row,
            // the second array's reads carried the offset of its first row -
            // `icb[i + 3]` - and fxc, given an index it has to work out, spends an
            // instruction on it where the original folded the row into the read.
            foreach ((string name, int start, int length) in
                _registers.ImmediateConstantBufferArrays())
            {
                WriteLine("static const float4 {0}[{1}] =", name, length);
                WriteLine("{");
                indent = "	";
                foreach (ConstantRegister row in _registers.ImmediateConstantBuffer
                    .Skip(start).Take(length))
                {
                    string components = string.Join(", ", row.Value.Select(
                        v => v.ToString(CultureInfo.InvariantCulture)));
                    WriteLine($"float4({components}),");
                }
                indent = "";
                WriteLine("};");
                WriteLine();
            }
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
            foreach (ResourceDefinition resource in StructuredElementTypes())
            {
                WriteLine($"struct {GetStructuredElementTypeName(resource)}");
                WriteLine("{");
                indent = "	";
                foreach (ShaderStructMemberInfo member in resource.ElementType.MemberInfo)
                {
                    WriteLine(compiler.Compile(member));
                }
                indent = "";
                WriteLine("};");
                WriteLine();
            }

            int declared = 0;
            // fxc binds a texture or a sampler declared without a register to the
            // next free slot in declaration order, so one declared with a gap
            // before it - stencil at t1, scene at t3 - has to say where it goes,
            // or it comes back bound elsewhere: a different texture, and the app
            // none the wiser.
            int nextTexture = 0;
            int nextSampler = 0;
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
                    string slot = resource.BindPoint == nextTexture ? "" : $" : register(t{resource.BindPoint})";
                    nextTexture = resource.BindPoint + 1;
                    WriteLine($"{resource.TypeName} {resource.Name}{slot};");
                }
                else if (resource.ShaderInputType == D3DShaderInputType.Sampler)
                {
                    // SampleCmp and SampleCmpLevelZero only take the comparison kind,
                    // which the reflection data flags.
                    string samplerType = resource.Flags.HasFlag(D3DShaderInputFlags.ComparisonSampler)
                        ? "SamplerComparisonState"
                        : "SamplerState";
                    string slot = resource.BindPoint == nextSampler ? "" : $" : register(s{resource.BindPoint})";
                    nextSampler = resource.BindPoint + 1;
                    WriteLine($"{samplerType} {resource.Name}{slot};");
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
                // no bare RWTexture2D that means RWTexture2D<float4>. Written by an
                // interlocked operation, its element is the scalar that operation
                // needs, which the source must have had.
                else if (resource.ShaderInputType == D3DShaderInputType.UavRWTyped)
                {
                    string viewTypeName = _registers.IsAtomicTarget(resource)
                        ? resource.ReadWriteAtomicTypeName
                        : resource.ReadWriteTypeName;
                    WriteLine($"{viewTypeName} {resource.Name} : register(u{resource.BindPoint});");
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
            ShaderType.Hull => "HS_IN",
            _ => throw new NotImplementedException(_shader.Type.ToString()),
        };
    }

    /// <summary>
    /// What the shader returns when it returns a struct. A hull shader has two, one
    /// per function: the control point it computes, and the constants of the whole
    /// patch.
    /// </summary>
    protected string GetOutputStructureName()
    {
        if (_hullFunction == HullFunction.PatchConstant)
        {
            return PatchConstants.StructureName(ShaderType.Hull);
        }
        return _shader.Type switch
        {
            ShaderType.Pixel => "PS_OUT",
            ShaderType.Vertex => "VS_OUT",
            ShaderType.Geometry => "GS_OUT",
            ShaderType.Hull => "HS_OUT",
            ShaderType.Domain => "DS_OUT",
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
        // A hull shader's control point phase reads the same patch the same way,
        // and which control point it is computing is a parameter rather than a
        // field of the point.
        if (_shader.Type is ShaderType.Domain or ShaderType.Hull)
        {
            inputs = [.. inputs
                .Where(r => r.RegisterKey is D3D10RegisterKey
                    { OperandType: not OperandType.InputDomainPoint
                        and not OperandType.OutputControlPointID })];
        }
        if (_shader.Type is ShaderType.Geometry or ShaderType.Domain or ShaderType.Hull)
        {
            // One member per register across the vertices - and per semantic
            // within a register, where two are packed into one. Grouping a patch's
            // registers by the register alone, as the domain and hull shaders did,
            // lost the packed semantic: `float3 position; float thickness;` is
            // v[2][0].xyz and v[2][0].w, and the struct came out holding the
            // position alone.
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
        WriteLine($"struct {PatchConstants.StructureName(_shader.Type)}");
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
        // A domain shader among them: it answers a vertex, and a vertex is a
        // position and whatever else travels with it. Left out, one that answers
        // anything beyond a bare position had no struct to return and no name for
        // one, and threw rather than decompiling.
        if (_shader.Type is not (ShaderType.Pixel or ShaderType.Vertex
            or ShaderType.Geometry or ShaderType.Hull or ShaderType.Domain))
        {
            return;
        }
        // A geometry shader writing several streams has a vertex of its own for each,
        // and both call their registers o0 upwards: one struct apiece, or the fields of
        // the two came out in the one struct with the same names twice over.
        if (_registers.HasSeveralStreams)
        {
            foreach (int stream in _registers.Streams)
            {
                WriteStreamStructureDeclaration(stream);
            }
            return;
        }
        string outputStructType = GetOutputStructureName();

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

    private void WriteStreamStructureDeclaration(int stream)
    {
        WriteLine($"struct {_registers.StreamStructureName(stream)}");
        WriteLine("{");
        indent = "\t";
        foreach (RegisterDeclaration output in _registers.MethodOutputRegisters
            .Where(o => (o.RegisterKey as D3D10RegisterKey)?.Stream == stream))
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
        // The patch constant function returns the struct however few registers it
        // fills: its fields are the tessellation factors, and those are an array
        // across registers rather than one value with one semantic.
        if (_hullFunction == HullFunction.PatchConstant)
        {
            return GetOutputStructureName();
        }
        return _registers.MethodOutputRegisters.Count switch
        {
            0 => "void",
            1 => _registers.MethodOutputRegisters.First().TypeName,
            _ => GetOutputStructureName(),
        };
    }

    /// <summary>
    /// Whether the outputs are written through a struct rather than returned as the
    /// one expression. A geometry shader always is: it writes its vertices through
    /// the stream, so the body says `o.member` however few members there are.
    /// </summary>
    protected bool HasOutputStruct =>
        _registers.MethodOutputRegisters.Count > 1
        || _shader.Type == ShaderType.Geometry
        || _hullFunction == HullFunction.PatchConstant;

    private string GetMethodSemantic()
    {
        // `void main(...) : SV_Position` is an error - X3076, a void function cannot
        // have a semantic - and a geometry or compute shader returns void whatever
        // its one output register might have suggested.
        if (GetMethodReturnType() != "void" && !HasOutputStruct
            && _registers.MethodOutputRegisters.Count == 1)
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
            string stream = _registers.HasSeveralStreams
                ? string.Join(", ", _registers.Streams.Select(s =>
                    $"inout {GetStreamType(_registers.TopologyByStream[s])}"
                        + $"<{_registers.StreamStructureName(s)}> {_registers.StreamParameterName(s)}"))
                : $"inout {GetStreamType(_registers.PrimitiveTopology)}<GS_OUT> stream";
            string primitiveId = _registers.PrimitiveIdDeclaration == null
                ? ""
                : $"{CompileRegisterDeclaration(_registers.PrimitiveIdDeclaration)}, ";
            string instanceId = _registers.GSInstanceIdDeclaration == null
                ? ""
                : $"{CompileRegisterDeclaration(_registers.GSInstanceIdDeclaration)}, ";
            return $"{primitive} GS_IN i[{vertexCount}], {primitiveId}{instanceId}{stream}";
        }
        if (_shader.Type == ShaderType.Hull)
        {
            // Both functions are handed the patch the tessellator is about to divide.
            // The control point phase is told which point it is computing as well;
            // the patch constant function is run once for the patch and is not.
            var parameters = new List<string>
            {
                $"InputPatch<{GetInputStructureName()}, {_registers.InputControlPointCount}> patch",
            };
            if (_registers.PrimitiveIdDeclaration != null)
            {
                parameters.Add(CompileRegisterDeclaration(_registers.PrimitiveIdDeclaration));
            }
            RegisterDeclaration controlPointId = _registers.MethodInputRegisters
                .FirstOrDefault(r => r.RegisterKey is D3D10RegisterKey
                    { OperandType: OperandType.OutputControlPointID });
            if (controlPointId != null)
            {
                parameters.Add(CompileRegisterDeclaration(controlPointId));
            }
            return string.Join(", ", parameters);
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
            return $"{PatchConstants.StructureName(_shader.Type)} {PatchConstants.ParameterName}, {domainLocation}"
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
    /// <summary>
    /// Whether the buffer's variables sit where declaring them in this order would
    /// put them. A packoffset moves one, or leaves a register empty in front of it,
    /// and the declaration alone then says something the shader did not: the buffer
    /// it is handed is laid out one way and the registers it reads are another.
    /// Where the two agree - which is every buffer fxc packed itself - nothing is
    /// said, and the declaration stays the one the shader was written with.
    /// </summary>
    private static bool NeedsPackOffset(IEnumerable<D3D10ConstantDeclaration> members)
    {
        const int RegisterSize = 4 * sizeof(float);
        int cursor = 0;
        foreach (D3D10ConstantDeclaration member in members)
        {
            int size = member.VariableSize;
            // An array, a matrix and a struct begin a register of their own; a
            // scalar or a vector packs in beside what is already there unless it
            // would run over the end of the register, which none of them may do.
            bool beginsARegister = member.TypeInfo.NumElements > 1
                || member.TypeInfo.Rows > 1
                || member.TypeInfo.MemberInfo != null;
            if (beginsARegister
                || cursor / RegisterSize != (cursor + size - 1) / RegisterSize)
            {
                cursor = (cursor + RegisterSize - 1) / RegisterSize * RegisterSize;
            }
            if (cursor != member.VariableOffset)
            {
                return true;
            }
            cursor += size;
        }
        return false;
    }

    private static string PackOffset(int offset)
    {
        string component = (offset % 16 / 4) switch
        {
            1 => ".y",
            2 => ".z",
            3 => ".w",
            _ => "",
        };
        return $"packoffset(c{offset / 16}{component})";
    }

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