using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace HlslDecompiler;

public abstract class HlslWriter
{
    protected readonly ShaderModel _shader;

    protected TextWriter internalWriter;
    protected string indent = "";

    protected HlslAst _ast;
    protected RegisterState _registers;

    /// <summary>
    /// Which of a hull shader's two functions is being written; they differ in
    /// return type and in what their output registers mean.
    /// </summary>
    protected enum HullFunction
    {
        None,
        ControlPoint,
        PatchConstant,
    }

    protected HullFunction _hullFunction = HullFunction.None;

    /// <summary>
    /// The bytecode being written: the whole shader, or one hull shader phase with
    /// the shared declarations, so a phase writes only its own body.
    /// </summary>
    protected ShaderModel _phaseShader;

    /// <summary>
    /// Whether every bytecode if/loop is written [branch]/[loop]. Off, an attribute
    /// is written only where fxc needs it, leaving fxc free to flatten and unroll.
    /// </summary>
    public bool WritesFlowAttributes { get; init; }

    public HlslWriter(ShaderModel shader)
    {
        // A ps_1_x shader is written as the equivalent ps_2_0 one.
        _shader = PixelShader1Lowering.Lower(shader);
    }

    /// <summary>
    /// Set for a shader inside an effect: the function name replacing main and
    /// prefixing its structs; uniforms and resources are left to the effect.
    /// </summary>
    public string FunctionName { get; init; }

    private bool IsEffectFunction => FunctionName != null;

    private string EntryName => FunctionName ?? "main";

    // VS_IN, or vs_main_VS_IN in an effect.
    protected string Named(string name) => IsEffectFunction ? $"{FunctionName}_{name}" : name;

    protected string StreamStructureName(int? stream) => Named(_registers.StreamStructureName(stream));

    /// <summary>
    /// Each resource's declared type, for the effect: its own types lack the
    /// element type (Texture2D, not Texture2D&lt;uint4&gt;).
    /// </summary>
    public Dictionary<string, string> ResourceTypeNames { get; } = [];

    /// <summary>
    /// Declares every input signature element, read or not, for stages linked to
    /// the previous one by register; dropping an unread one shifts later registers.
    /// Per-vertex inputs are declared from one vertex's key.
    /// </summary>
    private void DeclareWholeInputSignature()
    {
        if (_shader.Type is not (ShaderType.Pixel or ShaderType.Geometry or ShaderType.Hull or ShaderType.Domain))
        {
            return;
        }
        bool isPerVertex = _shader.Type != ShaderType.Pixel;
        D3D10RegisterKey vertexKey = _registers.MethodInputRegisters
            .Select(d => d.RegisterKey as D3D10RegisterKey)
            .FirstOrDefault(k => k?.GSVertex != null);

        static int Register(RegisterDeclaration d) => ((D3D10RegisterKey)d.RegisterKey).GetGSBaseKey().Number;
        bool IsInput(RegisterDeclaration d) => d.RegisterKey is D3D10RegisterKey key
            && (isPerVertex ? key.GSVertex != null : key.OperandType == OperandType.Input);

        foreach (RegisterSignature signature in _shader.InputSignatures)
        {
            // Per-primitive values are parameters, not fields.
            if (isPerVertex && signature.Name.ToUpperInvariant() is "SV_PRIMITIVEID" or "SV_GSINSTANCEID"
                or "SV_OUTPUTCONTROLPOINTID" or "SV_DOMAINLOCATION")
            {
                continue;
            }
            string semantic = signature.Index == 0 ? signature.Name : signature.Name + signature.Index;
            int register = signature.RegisterKey.Number;
            bool isDeclared = _registers.MethodInputRegisters.Any(d =>
                IsInput(d) && Register(d) == register
                && string.Equals(d.Semantic, semantic, StringComparison.OrdinalIgnoreCase));
            if (isDeclared)
            {
                continue;
            }

            D3D10RegisterKey key = !isPerVertex
                ? signature.RegisterKey
                : vertexKey?.WithAttribute(register) ?? D3D10RegisterKey.CreateGSInput(register, 0);
            var keyed = new RegisterSignature(key, signature.Name, signature.Index, signature.Mask,
                signature.ValueType, signature.ComponentType, signature.ReadWriteMask)
            {
                MinPrecision = signature.MinPrecision,
            };
            RegisterDeclaration declaration = FromSignature(keyed);
            // A register shared with a declared element holds both, each its own width.
            if (_registers.MethodInputRegisters.FirstOrDefault(d => IsInput(d) && Register(d) == register)
                is RegisterDeclaration neighbour)
            {
                declaration.MaskedLengthOverride = BitOperations.PopCount((uint)signature.Mask);
                neighbour.MaskedLengthOverride ??= BitOperations.PopCount((uint)neighbour.WriteMask);
            }
            _registers.MethodInputRegisters.Add(declaration);
            _registers.RegisterDeclarations.TryAdd(key, declaration);
        }

        // Register order is fxc's struct layout; non-inputs (ids) go after.
        List<RegisterDeclaration> ordered = [.. _registers.MethodInputRegisters
            .OrderBy(d => IsInput(d) ? 0 : 1)
            .ThenBy(d => IsInput(d) ? Register(d) : 0)
            .ThenBy(d => BitOperations.TrailingZeroCount(d.WriteMask))];
        _registers.MethodInputRegisters.Clear();
        foreach (RegisterDeclaration declaration in ordered)
        {
            _registers.MethodInputRegisters.Add(declaration);
        }
    }

    /// <summary>
    /// Each uniform's type from the SM3 constant tables; an fx_2_0 effect's own
    /// parameters call every matrix row_major.
    /// </summary>
    public Dictionary<string, ShaderTypeInfo> ConstantTypes { get; } = [];

    private void RecordResourceTypeNames()
    {
        foreach (ResourceDefinition resource in _registers.ResourceDefinitions ?? [])
        {
            if (ResourceTypeName(resource) is string typeName)
            {
                ResourceTypeNames.TryAdd(resource.Name, typeName);
            }
        }
        foreach (D3D9ConstantDeclaration constant in _registers.ConstantDeclarations.OfType<D3D9ConstantDeclaration>())
        {
            ConstantTypes.TryAdd(constant.Name, constant.TypeInfo);
        }
    }

    /// <summary>
    /// The type a resource is declared with, or null for one that is not declared
    /// as a resource of its own: a texture buffer is its block.
    /// </summary>
    private string ResourceTypeName(ResourceDefinition resource)
    {
        return resource.ShaderInputType switch
        {
            D3DShaderInputType.TBuffer => null,
            D3DShaderInputType.Texture => resource.TypeName,
            D3DShaderInputType.Sampler => resource.Flags.HasFlag(D3DShaderInputFlags.ComparisonSampler)
                ? "SamplerComparisonState"
                : "SamplerState",
            D3DShaderInputType.Structured => $"StructuredBuffer<{GetStructuredElementType(resource)}>",
            D3DShaderInputType.UavRWStructured => $"RWStructuredBuffer<{GetStructuredElementType(resource)}>",
            D3DShaderInputType.ByteAddress => "ByteAddressBuffer",
            // Only reflection tells these apart: the bytecode says
            // dcl_uav_structured for all three.
            D3DShaderInputType.UavAppendStructured => $"AppendStructuredBuffer<{GetStructuredElementType(resource)}>",
            D3DShaderInputType.UavConsumeStructured => $"ConsumeStructuredBuffer<{GetStructuredElementType(resource)}>",
            D3DShaderInputType.UavRWStucturedWithCounter => $"RWStructuredBuffer<{GetStructuredElementType(resource)}>",
            D3DShaderInputType.UavRWByteAddress => "RWByteAddressBuffer",
            // A typed UAV always names its element type; an interlocked target's
            // element is the scalar the operation needs.
            D3DShaderInputType.UavRWTyped => _registers.IsAtomicTarget(resource)
                ? resource.ReadWriteAtomicTypeName
                : resource.ReadWriteTypeName,
            _ => throw new NotImplementedException(),
        };
    }

    private string PatchConstantFunctionName => Named(PatchConstants.FunctionName);

    private string PatchConstantStructureName(ShaderType type) => Named(PatchConstants.StructureName(type));

    protected abstract void WriteMethodBody();

    /// <summary>
    /// Whether this writer mirrors instructions one by one rather than the value
    /// graph, so it still needs declarations the graph dropped.
    /// </summary>
    protected virtual bool WritesInstructions => false;

    protected void WriteLine()
    {
        internalWriter.WriteLine();
    }

    protected void WriteLine(string value)
    {
        // A compiled statement can be two lines; each gets the indent.
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
        // Always CRLF, so output is the same on every host.
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
        NameLocals();

        RecordResourceTypeNames();
        DeclareWholeInputSignature();
        WriteConstantDeclarations();
        WriteThreadGroupSharedMemoryDeclarations();

        // GS/DS signatures name the input struct even for one register.
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

        if (_registers.ForceEarlyDepthStencil)
        {
            WriteLine("[earlydepthstencil]");
        }

        if (_registers.Linkage != null && _registers.Linkage.HasLinkage)
        {
            WriteLinkageDeclarations();
        }

        string methodReturnType = GetMethodReturnType();
        string methodParameters = GetMethodParameters();
        string methodSemantic = GetMethodSemantic();

        WriteFunction($"{methodReturnType} {EntryName}({methodParameters}){methodSemantic}",
            WriteMethodBody);
    }

    /// <summary>
    /// Writes dynamic linkage declarations in the order HLSL requires: interfaces,
    /// classes implementing them, then the interface globals fcalls go through.
    /// </summary>
    private void WriteLinkageDeclarations()
    {
        LinkageModel linkage = _registers.Linkage;
        if (linkage.Bodies.Any(b => b.Parameters.Count != 0)
            && _registers.MethodInputRegisters.Count > 1)
        {
            throw new NotImplementedException(
                "a linkage method argument named through an input struct");
        }

        foreach (LinkageModel.InterfaceTypeInfo type in linkage.InterfaceTypes)
        {
            WriteLine($"interface {type.Name}");
            WriteLine("{");
            indent = "\t";
            foreach (LinkageModel.MethodInfo method in type.Methods)
            {
                WriteLine($"{LinkageMethodSignature(method)};");
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }

        WriteLinkageClasses();

        foreach (LinkageModel.InterfaceInfo iface in linkage.Interfaces)
        {
            WriteLine($"{iface.Type.Name} {iface.InstanceName}"
                + $"{(iface.IsArray ? $"[{iface.InstanceArrayLength}]" : "")};");
        }
        WriteLine();
    }

    /// <summary>
    /// The classes, each implementing every interface method with the body its
    /// tables hold; each writer fills the bodies its own way.
    /// </summary>
    protected void WriteLinkageClasses()
    {
        foreach (LinkageModel.ClassInfo classInfo in _registers.Linkage.Classes)
        {
            WriteLine("class {0} : {1}", classInfo.Name,
                string.Join(", ", classInfo.Implements.Select(t => t.Name)));
            WriteLine("{");
            indent = "\t";
            bool first = true;
            foreach (LinkageModel.MethodInfo method in classInfo.Implements
                .SelectMany(t => t.Methods))
            {
                if (!first)
                {
                    WriteLine();
                }
                first = false;
                WriteLine(LinkageMethodSignature(method));
                WriteLine("{");
                indent = "\t\t";
                WriteLinkageMethodBody(classInfo.Bodies[method]);
                indent = "\t";
                WriteLine("}");
            }
            indent = "";
            WriteLine("};");
            WriteLine();
        }
    }

    protected abstract void WriteLinkageMethodBody(LinkageModel.FunctionBodyInfo body);

    protected string LinkageMethodSignature(LinkageModel.MethodInfo method)
    {
        string parameters = string.Join(", ", method.Parameters.Select(parameter =>
        {
            RegisterDeclaration declaration = _registers.MethodInputRegisters
                .FirstOrDefault(d => d.RegisterKey.Equals(parameter));
            if (declaration == null)
            {
                throw new NotImplementedException(
                    "a linkage method argument with no input declaration");
            }
            return $"{declaration.TypeName} {declaration.Name}";
        }));
        return $"{LinkageReturnTypeName(method)} {method.Name}({parameters})";
    }

    /// <summary>
    /// Always float4; a body of any class computing an integer result throws
    /// rather than returning floats from an int4.
    /// </summary>
    private string LinkageReturnTypeName(LinkageModel.MethodInfo method)
    {
        foreach (LinkageModel.FunctionBodyInfo body in method.Bodies)
        {
            for (int i = body.First; i < body.Last; i++)
            {
                if (_phaseShader.Instructions[i] is D3D10Instruction instruction
                    && instruction.GetDestinationParamIndex() != null
                    && Equals(instruction.GetParamRegisterKey(
                        instruction.GetDestinationParamIndex().Value), body.ReturnRegister)
                    && instruction.Opcode.IsInteger())
                {
                    throw new NotImplementedException("an integer-returning linkage method");
                }
            }
        }
        return "float4";
    }

    /// <summary>
    /// Writes a hull shader as its control point and patch constant functions,
    /// with the shared declarations written once.
    /// </summary>
    private void WriteHullShader()
    {
        HullShaderAst hull = InstructionParser.ParseHullShader(_shader);

        // Structs come from the control point phase: patch constant phases
        // declare only the control point registers they touch.
        EnterPhase(HullFunction.ControlPoint, hull.ControlPoint ?? hull.PatchConstant);
        RecordResourceTypeNames();
        if (hull.ControlPoint != null)
        {
            DeclareWholeInputSignature();
        }
        WriteConstantDeclarations();
        WriteThreadGroupSharedMemoryDeclarations();
        if (hull.ControlPoint != null)
        {
            WriteInputStructureDeclaration();
            WriteOutputStructureDeclaration();
        }
        else
        {
            // No control point phase: the signatures describe the whole patch.
            WriteSignatureStructure(GetInputStructureName(), _shader.InputSignatures);
            WriteSignatureStructure(GetOutputStructureName(), _shader.OutputSignatures);
        }
        WritePatchConstantStructureDeclaration();

        if (hull.PatchConstant != null)
        {
            EnterPhase(HullFunction.PatchConstant, hull.PatchConstant);
            WriteFunction($"{GetMethodReturnType()} {PatchConstantFunctionName}"
                + $"({GetMethodParameters()})", WriteMethodBody);
            WriteLine();
        }

        if (hull.ControlPoint != null)
        {
            EnterPhase(HullFunction.ControlPoint, hull.ControlPoint);
            WriteTessellatorAttributes();
            WriteFunction(
                $"{GetMethodReturnType()} {EntryName}({GetMethodParameters()}){GetMethodSemantic()}",
                WriteMethodBody);
            return;
        }

        _hullFunction = HullFunction.ControlPoint;
        WriteTessellatorAttributes();
        string controlPointId = CompileRegisterDeclaration(ControlPointIdDeclaration());
        WriteFunction(
            $"{GetOutputStructureName()} {EntryName}(InputPatch<{GetInputStructureName()}, "
                + $"{_registers.InputControlPointCount}> patch, {controlPointId})",
            WritePassThroughControlPoint);
    }

    /// <summary>
    /// The pass-through control point function fxc omits from the bytecode,
    /// rebuilt from the matching input and output signatures.
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
    /// The SV_OutputControlPointID parameter, for when no phase declares it.
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
    /// A struct written from a signature chunk rather than from declarations.
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
            MinPrecision = signature.MinPrecision,
        };
    }

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
        NameLocals();
    }

    // In an effect, locals are prefixed with the function name.
    private void NameLocals()
    {
        if (IsEffectFunction)
        {
            _registers.LocalNamePrefix = FunctionName + "_";
        }
    }

    /// <summary>
    /// The hull shader's tessellator attributes. maxtessfactor must be a float
    /// literal: fxc ignores [maxtessfactor(32)] with X3554.
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
        WriteLine("[patchconstantfunc(\"{0}\")]", PatchConstantFunctionName);
        if (_registers.MaxTessFactor is float bound)
        {
            string literal = bound.ToString("R", CultureInfo.InvariantCulture);
            if (!literal.Contains('.') && !literal.Contains('E'))
            {
                literal += ".0";
            }
            WriteLine($"[maxtessfactor({literal})]");
        }
    }

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
        // In an effect only literal arrays are the function's own.
        if (IsEffectFunction)
        {
            WriteStaticConstants();
            return;
        }

        // One compiler for uniforms and structured buffer elements, so structN
        // numbering is shared and each struct is declared once.
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
            // The constant table lists a variable once per register set (i# and c#).
            var declaredNames = new HashSet<string>();
            int written = 0;
            foreach (ConstantDeclaration declaration in _registers.ConstantDeclarations)
            {
                // Written in their blocks below.
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

            // A named buffer keeps its block and slot; globals are flattened.
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

            // A tbuffer binds to a t register and is read with ld, so it keeps its block.
            foreach (var textureBuffer in _registers.ConstantDeclarations
                .OfType<D3D10ConstantDeclaration>()
                .Where(d => d.IsTextureBuffer)
                .GroupBy(d => d.BufferName))
            {
                WriteLine($"tbuffer {textureBuffer.Key}");
                WriteLine("{");
                indent = "\t";
                // Once per variable: the table lists one per register it occupies.
                var declaredMembers = new HashSet<string>();
                foreach (D3D10ConstantDeclaration member in textureBuffer
                    .Where(m => declaredMembers.Add(m.Name)))
                {
                    WriteLine(compiler.Compile(member));
                }
                indent = "";
                WriteLine("};");
                WriteLine();
            }
        }

        WriteStaticConstants();
        WriteResourceDeclarations(compiler);
    }

    private void WriteStaticConstants()
    {
        // Only while something reads it: identity-row reads may have become
        // vector subscripts, though the instruction writer still reads the rows.
        if (_registers.ImmediateConstantBuffer.Count != 0
            && (WritesInstructions || _registers.IsImmediateConstantBufferRead))
        {
            // One declaration per array, so reads need no row offset (`icb[i + 3]`),
            // which would cost fxc an instruction.
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
    }

    private void WriteResourceDeclarations(ConstantDeclarationCompiler compiler)
    {
        if (_registers.ResourceDefinitions != null && _registers.ResourceDefinitions.Count != 0)
        {
            // Struct element types are declared before their buffers, once each.
            HashSet<string> declaredStructs = [];
            foreach (ResourceDefinition resource in StructuredElementTypes())
            {
                if (!declaredStructs.Add(GetStructuredElementTypeName(resource)))
                {
                    continue;
                }
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
            // fxc binds an unregistered texture or sampler to the next free slot, so
            // one after a gap must name its register.
            int nextTexture = 0;
            int nextSampler = 0;
            var definitions = _registers.ResourceDefinitions.ToList();
            for (int r = 0; r < definitions.Count; r++)
            {
                var resource = definitions[r];
                string typeName = ResourceTypeName(resource);
                // A texture buffer is declared by its block, with the constants.
                if (typeName == null)
                {
                    continue;
                }
                // Reflection lists a resource array per element (layers[0], layers[1]);
                // a run of them in consecutive slots is declared as one array.
                string name = resource.Name;
                int count = 1;
                var element = System.Text.RegularExpressions.Regex.Match(name, @"^(\w+)\[0\]$");
                if (element.Success)
                {
                    while (r + count < definitions.Count
                        && definitions[r + count].Name == $"{element.Groups[1].Value}[{count}]"
                        && definitions[r + count].ShaderInputType == resource.ShaderInputType
                        && definitions[r + count].BindPoint == resource.BindPoint + count
                        && ResourceTypeName(definitions[r + count]) == typeName)
                    {
                        count++;
                    }
                    name = $"{element.Groups[1].Value}[{count}]";
                }
                string slot;
                if (resource.ShaderInputType == D3DShaderInputType.Texture)
                {
                    slot = resource.BindPoint == nextTexture ? "" : $" : register(t{resource.BindPoint})";
                    nextTexture = resource.BindPoint + count;
                }
                else if (resource.ShaderInputType == D3DShaderInputType.Sampler)
                {
                    slot = resource.BindPoint == nextSampler ? "" : $" : register(s{resource.BindPoint})";
                    nextSampler = resource.BindPoint + count;
                }
                else
                {
                    bool isView = resource.ShaderInputType is D3DShaderInputType.Structured or D3DShaderInputType.ByteAddress;
                    slot = $" : register({(isView ? 't' : 'u')}{resource.BindPoint})";
                }
                WriteLine($"{typeName} {name}{slot};");
                declared++;
                r += count - 1;
            }
            if (declared != 0)
            {
                WriteLine();
            }
        }
    }

    // A named constant buffer, as against $Globals.
    private static bool IsNamedBuffer(ConstantDeclaration declaration)
    {
        return declaration is D3D10ConstantDeclaration { IsTextureBuffer: false, BufferName: not null and not "$Globals" };
    }

    /// <summary>
    /// Declares groupshared arrays (g0[64], ...). The type is float unless every
    /// store into the array is an integer.
    /// </summary>
    private void WriteThreadGroupSharedMemoryDeclarations()
    {
        if (_registers.ThreadGroupSharedMemory.Count == 0)
        {
            return;
        }
        var integerOperandAnalysis = new IntegerOperandAnalysis(_shader);
        foreach ((int register, (int stride, int elements)) in _registers.ThreadGroupSharedMemory.OrderBy(t => t.Key))
        {
            if (stride < 4 || stride % sizeof(float) != 0)
            {
                throw new NotImplementedException($"groupshared element stride {stride}");
            }
            // uint where an unsigned interlocked min/max reaches it: HLSL picks
            // the signed or unsigned form from the type.
            string type = integerOperandAnalysis.IsIntegerThreadGroupSharedMemory(register)
                ? integerOperandAnalysis.IsUnsignedThreadGroupSharedMemory(register)
                    ? "uint"
                    : "int"
                : "float";
            int components = stride / sizeof(float);
            // Wider than a float4: a struct of four-wide members plus the remainder.
            if (components > 4)
            {
                string elementType = RegisterState.ThreadGroupSharedElementType(register);
                WriteLine($"struct {elementType}");
                WriteLine("{");
                foreach ((string name, _, int memberComponents) in
                    RegisterState.ThreadGroupSharedMembers(stride))
                {
                    string memberSize = memberComponents == 1
                        ? ""
                        : memberComponents.ToString(CultureInfo.InvariantCulture);
                    WriteLine($"	{type}{memberSize} {name};");
                }
                WriteLine("};");
                WriteLine();
                WriteLine($"groupshared {elementType} {_registers.GroupSharedName(register)}[{elements}];");
                continue;
            }
            string size = components == 1 ? "" : components.ToString(CultureInfo.InvariantCulture);
            // No subscript where fxc declared it raw: an array of one comes back
            // as a structured declaration.
            string bounds = _registers.IsGroupSharedScalar(register)
                ? ""
                : $"[{elements}]";
            WriteLine($"groupshared {type}{size} {_registers.GroupSharedName(register)}{bounds};");
        }
        WriteLine();
    }

    /// <summary>
    /// Declares indexable temps (x0[4], ...) as local arrays; float unless every
    /// write to the array is an integer instruction.
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

    /// <summary>A structured buffer element's struct name: its reflected one if
    /// valid and unclashing, else one made from the buffer's name.</summary>
    protected string GetStructuredElementTypeName(ResourceDefinition resource)
    {
        string own = resource.ElementType?.Name;
        if (!string.IsNullOrEmpty(own)
            && System.Text.RegularExpressions.Regex.IsMatch(own, "^[A-Za-z_][A-Za-z0-9_]*$")
            && !System.Text.RegularExpressions.Regex.IsMatch(own, "_(IN|OUT|CONST)$")
            && StructuredElementTypes().All(other => other.ElementType?.Name != own
                || SameMembers(other.ElementType, resource.ElementType)))
        {
            return own;
        }
        return char.ToUpperInvariant(resource.Name[0]) + resource.Name[1..] + "Element";
    }

    private static bool SameMembers(ShaderTypeInfo a, ShaderTypeInfo b)
    {
        return ReferenceEquals(a, b)
            || (a.MemberInfo.Count == b.MemberInfo.Count
                && a.MemberInfo.Zip(b.MemberInfo).All(pair => pair.First.Name == pair.Second.Name));
    }

    /// <summary>
    /// The element type: from reflection where present, else float of the
    /// declared stride's width.
    /// </summary>
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

    private string GetInputStructureName()
    {
        return Named(_shader.Type switch
        {
            ShaderType.Pixel => "PS_IN",
            ShaderType.Vertex => "VS_IN",
            ShaderType.Geometry => "GS_IN",
            ShaderType.Compute => "CS_IN",
            ShaderType.Domain => "DS_IN",
            ShaderType.Hull => "HS_IN",
            _ => throw new NotImplementedException(_shader.Type.ToString()),
        });
    }

    /// <summary>
    /// The output struct name; a hull shader's patch constant function has its own.
    /// </summary>
    protected string GetOutputStructureName()
    {
        if (_hullFunction == HullFunction.PatchConstant)
        {
            return PatchConstantStructureName(ShaderType.Hull);
        }
        return Named(_shader.Type switch
        {
            ShaderType.Pixel => "PS_OUT",
            ShaderType.Vertex => "VS_OUT",
            ShaderType.Geometry => "GS_OUT",
            ShaderType.Hull => "HS_OUT",
            ShaderType.Domain => "DS_OUT",
            _ => throw new NotImplementedException(_shader.Type.ToString()),
        });
    }

    private void WriteInputStructureDeclaration()
    {
        string inputStructType = GetInputStructureName();
        WriteLine($"struct {inputStructType}");
        WriteLine("{");
        indent = "\t";
        ICollection<RegisterDeclaration> inputs = _registers.MethodInputRegisters;
        // The struct is one control point; the domain location and control point
        // id are parameters, not fields.
        if (_shader.Type is ShaderType.Domain or ShaderType.Hull)
        {
            inputs = [.. inputs
                .Where(r => r.RegisterKey is D3D10RegisterKey
                    { OperandType: not OperandType.InputDomainPoint
                        and not OperandType.OutputControlPointID })];
        }
        if (_shader.Type is ShaderType.Geometry or ShaderType.Domain or ShaderType.Hull)
        {
            // One member per register and semantic: two semantics can share a
            // register (v[2][0].xyz and v[2][0].w).
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
    /// The patch constant struct, from the signature chunk: fxc refuses a domain
    /// shader missing the tessellation factors even when unread. Without the chunk,
    /// the factors the domain implies.
    /// </summary>
    private void WritePatchConstantStructureDeclaration()
    {
        WriteLine($"struct {PatchConstantStructureName(_shader.Type)}");
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
        if (_shader.Type is not (ShaderType.Pixel or ShaderType.Vertex
            or ShaderType.Geometry or ShaderType.Hull or ShaderType.Domain))
        {
            return;
        }
        // One struct per stream: each stream numbers its registers from o0.
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
        foreach (var output in OutputsInStructOrder())
        {
            WriteLine(CompileRegisterDeclaration(output) + ';');
        }
        indent = "";
        WriteLine("};");
        WriteLine();
    }

    /// <summary>
    /// The outputs in struct order: declaration order from SM3, by semantic before
    /// that, where fixed output registers (oPos, oT0) are never declared.
    /// </summary>
    protected IList<RegisterDeclaration> OutputsInStructOrder()
    {
        IList<RegisterDeclaration> outputs = _registers.MethodOutputRegisters;
        return _shader.MajorVersion <= 2 ? [.. outputs.OrderBy(o => o.Semantic)] : outputs;
    }

    private void WriteStreamStructureDeclaration(int stream)
    {
        WriteLine($"struct {StreamStructureName(stream)}");
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
        // Always a struct: the tessellation factors span registers.
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
    /// Whether outputs go through a struct; always for a geometry shader, which
    /// writes vertices through the stream.
    /// </summary>
    protected bool HasOutputStruct =>
        _registers.MethodOutputRegisters.Count > 1
        || _shader.Type == ShaderType.Geometry
        || _hullFunction == HullFunction.PatchConstant;

    private string GetMethodSemantic()
    {
        // A void function cannot have a semantic (X3076).
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
                        + $"<{StreamStructureName(s)}> {_registers.StreamParameterName(s)}"))
                : $"inout {GetStreamType(_registers.PrimitiveTopology)}<{GetOutputStructureName()}> stream";
            string primitiveId = _registers.PrimitiveIdDeclaration == null
                ? ""
                : $"{CompileRegisterDeclaration(_registers.PrimitiveIdDeclaration)}, ";
            string instanceId = _registers.GSInstanceIdDeclaration == null
                ? ""
                : $"{CompileRegisterDeclaration(_registers.GSInstanceIdDeclaration)}, ";
            return $"{primitive} {GetInputStructureName()} i[{vertexCount}], {primitiveId}{instanceId}{stream}";
        }
        if (_shader.Type == ShaderType.Hull)
        {
            var parameters = new List<string>
            {
                $"InputPatch<{GetInputStructureName()}, {_registers.InputControlPointCount}> patch",
            };
            // The patch constant function reads main's outputs as an OutputPatch of HS_OUT.
            if (_registers.ControlPointOutputRegisters.Count != 0)
            {
                parameters.Add($"OutputPatch<{Named("HS_OUT")}, "
                    + $"{_registers.OutputControlPointCount}> {RegisterState.OutputPatchName}");
            }
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
            RegisterDeclaration location = _registers.MethodInputRegisters
                .FirstOrDefault(r => r.RegisterKey is D3D10RegisterKey
                    { OperandType: OperandType.InputDomainPoint });
            string domainLocation = location == null
                ? ""
                : CompileRegisterDeclaration(location) + ", ";
            return $"{PatchConstantStructureName(_shader.Type)} {PatchConstants.ParameterName}, {domainLocation}"
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

    /// <summary>
    /// Whether the members sit anywhere other than where default packing in this
    /// order would put them, so the declaration needs packoffsets.
    /// </summary>
    private static bool NeedsPackOffset(IEnumerable<D3D10ConstantDeclaration> members)
    {
        const int RegisterSize = 4 * sizeof(float);
        int cursor = 0;
        foreach (D3D10ConstantDeclaration member in members)
        {
            int size = member.VariableSize;
            // Arrays, matrices and structs start a register; a scalar or vector
            // packs in unless it would cross a register boundary.
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

    // A geometry shader emits only strips, so the topology names the stream type.
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