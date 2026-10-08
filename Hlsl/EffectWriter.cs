using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A Direct3D 10 or 11 effect written back as the .fx it was compiled from: its
/// uniforms and objects, a function for each of its shaders, and the techniques
/// and passes that set them.
///
/// The order is the effect's own wherever HLSL allows it, since the order is what
/// the effect compiler numbers things by - variables, state objects and shaders
/// alike. A shader's function has to be declared before the variable that compiles
/// it and after the uniforms it reads, and the effect's order already has those the
/// right way round: each function is written just before the first variable that
/// needs it, and those only a pass compiles come after the variables.
/// </summary>
public sealed class EffectWriter
{
    private readonly Effect _effect;
    private readonly bool _doAstAnalysis;
    private readonly bool _writesFlowAttributes;

    private readonly Dictionary<EffectShader, string> _functionNames = [];
    private readonly Dictionary<string, string> _functionBodies = [];
    private readonly Dictionary<string, string> _functionsByBytecode = [];
    private readonly HashSet<string> _written = [];
    private readonly Dictionary<string, string> _resourceTypeNames = [];

    private TextWriter _writer;
    private string _indent = "";

    public EffectWriter(Effect effect, bool doAstAnalysis, bool writesFlowAttributes = false)
    {
        _writesFlowAttributes = writesFlowAttributes;
        _effect = effect;
        _doAstAnalysis = doAstAnalysis;
    }

    public void Write(string filename)
    {
        using var writer = new StreamWriter(filename);
        Write(writer);
    }

    public void Write(TextWriter writer)
    {
        writer.NewLine = "\r\n";
        _writer = writer;
        NameShaders();

        WriteStructTypes();
        WriteConstantBuffers();
        foreach (EffectObjectVariable variable in _effect.ObjectVariables)
        {
            foreach (EffectShader shader in variable.Shaders)
            {
                WriteFunction(shader);
            }
            WriteObjectVariable(variable);
        }
        foreach (string name in _functionBodies.Keys.Where(name => !_written.Contains(name)).ToList())
        {
            WriteFunction(name);
        }
        foreach (EffectGroup group in _effect.Groups)
        {
            WriteGroup(group);
        }
    }

    /// <summary>
    /// A function for every shader, named after where it is set: the variable that
    /// holds it, or the pass that compiles it. Two that are the same bytecode are
    /// one function - a shader compiled in two passes is.
    /// </summary>
    private void NameShaders()
    {
        foreach (EffectObjectVariable variable in _effect.ObjectVariables)
        {
            for (int i = 0; i < variable.Shaders.Count; i++)
            {
                string suffix = variable.Shaders.Count > 1 || variable.Type.Elements > 0 ? i.ToString(CultureInfo.InvariantCulture) : "";
                NameShader(variable.Shaders[i], $"{variable.Name}{suffix}");
            }
        }
        foreach (EffectGroup group in _effect.Groups)
        {
            foreach (EffectTechnique technique in group.Techniques)
            {
                foreach (EffectPass pass in technique.Passes)
                {
                    foreach (EffectAssignment assignment in pass.Assignments.Where(a => a.Shader != null))
                    {
                        string place = string.Join("_", new[] { group.Name, technique.Name, pass.Name }.Where(n => n != null));
                        NameShader(assignment.Shader, place);
                    }
                }
            }
        }
    }

    private void NameShader(EffectShader shader, string place)
    {
        if (shader.Bytecode == null)
        {
            return;
        }
        string key = Convert.ToBase64String(shader.Bytecode);
        if (_functionsByBytecode.TryGetValue(key, out string existing))
        {
            _functionNames[shader] = existing;
            return;
        }

        ShaderModel model = ReadShader(shader);
        string name = $"{model.Stage}_{place}";
        while (_functionBodies.ContainsKey(name))
        {
            name += "_";
        }

        HlslWriter writer = _doAstAnalysis
            ? new HlslAstWriter(model) { FunctionName = name, WritesFlowAttributes = _writesFlowAttributes }
            : new HlslSimpleWriter(model) { FunctionName = name, WritesFlowAttributes = _writesFlowAttributes };
        var body = new StringWriter();
        writer.Write(body);
        foreach ((string texture, string typeName) in writer.ResourceTypeNames)
        {
            _resourceTypeNames.TryAdd(texture, typeName);
        }

        _functionNames[shader] = name;
        _functionsByBytecode[key] = name;
        _functionBodies[name] = body.ToString();
    }

    private static ShaderModel ReadShader(EffectShader shader)
    {
        using var reader = new DxbcReader(new MemoryStream(shader.Bytecode));
        return reader.ReadShader();
    }

    private void WriteFunction(EffectShader shader)
    {
        if (_functionNames.TryGetValue(shader, out string name))
        {
            WriteFunction(name);
        }
    }

    private void WriteFunction(string name)
    {
        if (_written.Add(name))
        {
            Separate();
            WriteLine(_functionBodies[name].TrimEnd());
            Separate();
        }
    }

    // The struct types the uniforms are declared with, each before the first that
    // holds it.
    private void WriteStructTypes()
    {
        var written = new HashSet<string>();
        foreach (EffectNumericVariable variable in _effect.ConstantBuffers.SelectMany(b => b.Variables))
        {
            WriteStructType(variable.Type, written);
        }
    }

    private void WriteStructType(EffectType type, HashSet<string> written)
    {
        if (type.Class != EffectVariableClass.Struct || !written.Add(type.Name))
        {
            return;
        }
        foreach (EffectMember member in type.Members)
        {
            WriteStructType(member.Type, written);
        }
        WriteLine($"struct {type.Name}");
        WriteLine("{");
        _indent = "\t";
        foreach (EffectMember member in type.Members)
        {
            WriteLine($"{TypeName(member.Type)} {member.Name}{ArraySuffix(member.Type)}{Semantic(member.Semantic)};");
        }
        _indent = "";
        WriteLine("};");
        Separate();
    }

    private void WriteConstantBuffers()
    {
        foreach (EffectConstantBuffer buffer in _effect.ConstantBuffers)
        {
            // The globals are a buffer of their own to the compiler, named $Globals,
            // and declared by not being in any other.
            bool isGlobals = buffer.Name == "$Globals" && !buffer.IsTextureBuffer;
            if (!isGlobals)
            {
                string register = buffer.ExplicitBindPoint != -1
                    ? $" : register({(buffer.IsTextureBuffer ? 't' : 'b')}{buffer.ExplicitBindPoint})"
                    : "";
                WriteLine($"{(buffer.IsTextureBuffer ? "tbuffer" : "cbuffer")} {buffer.Name}{register}{Annotations(buffer.Annotations)}");
                WriteLine("{");
                _indent = "\t";
            }
            foreach (EffectNumericVariable variable in buffer.Variables)
            {
                string packOffset = variable.HasExplicitBindPoint
                    ? $" : packoffset(c{variable.Offset / 16}.{"xyzw"[variable.Offset % 16 / 4]})"
                    : "";
                string value = variable.DefaultValue != null
                    ? $" = {Value(variable.Type, variable.DefaultValue)}"
                    : "";
                WriteLine($"{TypeName(variable.Type)} {variable.Name}{ArraySuffix(variable.Type)}"
                    + $"{Semantic(variable.Semantic)}{packOffset}{Annotations(variable.Annotations)}{value};");
            }
            if (!isGlobals)
            {
                _indent = "";
                WriteLine("};");
            }
            Separate();
        }
    }

    private void WriteObjectVariable(EffectObjectVariable variable)
    {
        EffectType type = variable.Type;
        string declaration = $"{ObjectTypeName(variable)} {variable.Name}{ArraySuffix(type)}"
            + Semantic(variable.Semantic) + BindPoint(variable) + Annotations(variable.Annotations);

        if (type.IsStateBlock)
        {
            Separate();
            if (variable.Blocks.Count == 1 && type.Elements == 0)
            {
                WriteLine(declaration);
                WriteStateBlock(variable.Blocks[0], ";");
            }
            else
            {
                WriteLine(declaration + " =");
                WriteLine("{");
                _indent = "\t";
                foreach (IReadOnlyList<EffectAssignment> block in variable.Blocks)
                {
                    WriteStateBlock(block, ",");
                }
                _indent = "";
                WriteLine("};");
            }
            Separate();
        }
        else if (type.IsShader)
        {
            IEnumerable<string> shaders = variable.Shaders.Select(s => ShaderExpression(s, IsShader5(type.ObjectType)));
            WriteLine(type.Elements == 0
                ? $"{declaration} = {shaders.Single()};"
                : $"{declaration} = {{ {string.Join(", ", shaders)} }};");
        }
        else if (type.ObjectType == EffectObjectType.String)
        {
            IEnumerable<string> strings = variable.Strings.Select(Quoted);
            WriteLine(type.Elements == 0
                ? $"{declaration} = {strings.Single()};"
                : $"{declaration} = {{ {string.Join(", ", strings)} }};");
        }
        else
        {
            WriteLine(declaration + ";");
        }
    }

    // A texture's element type is not in the effect's type, only in the reflection
    // data of a shader that reads it. One no shader reads is the plain type.
    private string ObjectTypeName(EffectObjectVariable variable)
    {
        if (variable.Type.ObjectType == EffectObjectType.String)
        {
            // The effect spells the type String; HLSL spells it string.
            return "string";
        }
        return _resourceTypeNames.TryGetValue(variable.Name, out string typeName)
            ? typeName
            : variable.Type.Name;
    }

    private static string BindPoint(EffectObjectVariable variable)
    {
        if (variable.ExplicitBindPoint == -1)
        {
            return "";
        }
        char registerType = variable.Type.ObjectType switch
        {
            EffectObjectType.Sampler => 's',
            >= EffectObjectType.RWTexture1D and not EffectObjectType.ByteAddressBuffer and not EffectObjectType.StructuredBuffer => 'u',
            _ => 't',
        };
        return $" : register({registerType}{variable.ExplicitBindPoint})";
    }

    private void WriteStateBlock(IReadOnlyList<EffectAssignment> block, string terminator)
    {
        WriteLine("{");
        string outer = _indent;
        _indent += "\t";
        for (int i = 0; i < block.Count; i++)
        {
            EffectAssignment assignment = block[i];
            EffectState state = EffectStates.All[assignment.State];

            // fx_5_0 writes one value for every render target where the source
            // wrote it once without a subscript, so eight in a row that agree are
            // written back as the one.
            if (state.VectorScalar && assignment.Index == 0 && i + state.Indices <= block.Count
                && Enumerable.Range(0, state.Indices).All(n =>
                    block[i + n].State == assignment.State && block[i + n].Index == n
                    && StateValue(state, block[i + n]) == StateValue(state, assignment)))
            {
                WriteLine($"{state.Name} = {StateValue(state, assignment)};");
                i += state.Indices - 1;
                continue;
            }

            string index = state.Indices > 1 && !(state.VectorScalar && !_effect.IsFx5)
                ? $"[{assignment.Index}]"
                : "";
            WriteLine($"{state.Name}{index} = {StateValue(state, assignment)};");
        }
        _indent = outer;
        WriteLine("}" + terminator);
    }

    private void WriteGroup(EffectGroup group)
    {
        Separate();
        if (group.Name != null)
        {
            WriteLine($"fxgroup {group.Name}{Annotations(group.Annotations)}");
            WriteLine("{");
            _indent = "\t";
        }
        for (int t = 0; t < group.Techniques.Count; t++)
        {
            EffectTechnique technique = group.Techniques[t];
            if (t > 0)
            {
                Separate();
            }
            string outer = _indent;
            string keyword = _effect.IsFx5 ? "technique11" : "technique10";
            WriteLine($"{keyword}{(technique.Name != null ? " " + technique.Name : "")}{Annotations(technique.Annotations)}");
            WriteLine("{");
            _indent = outer + "\t";
            foreach (EffectPass pass in technique.Passes)
            {
                WriteLine($"pass{(pass.Name != null ? " " + pass.Name : "")}{Annotations(pass.Annotations)}");
                WriteLine("{");
                _indent = outer + "\t\t";
                WritePassAssignments(pass.Assignments);
                _indent = outer + "\t";
                WriteLine("}");
            }
            _indent = outer;
            WriteLine("}");
        }
        if (group.Name != null)
        {
            _indent = "";
            WriteLine("}");
        }
    }

    /// <summary>
    /// A pass's assignments as the calls that made them. SetBlendState is three -
    /// the blend factor, the sample mask, then the state - and SetDepthStencilState
    /// two, the stencil reference then the state, so those are gathered until the
    /// state they belong to comes.
    /// </summary>
    private void WritePassAssignments(IReadOnlyList<EffectAssignment> assignments)
    {
        string blendFactor = "float4(0, 0, 0, 0)";
        string sampleMask = "0xFFFFFFFF";
        string stencilReference = "0";
        foreach (EffectAssignment assignment in assignments)
        {
            EffectState state = EffectStates.All[assignment.State];
            switch (state.Name)
            {
                case "AB_BlendFactor":
                    blendFactor = StateValue(state, assignment);
                    break;
                case "AB_SampleMask":
                    sampleMask = StateValue(state, assignment);
                    break;
                case "DS_StencilRef":
                    stencilReference = StateValue(state, assignment);
                    break;
                case "BlendState":
                    WriteLine($"SetBlendState({ObjectValue(assignment)}, {blendFactor}, {sampleMask});");
                    break;
                case "DepthStencilState":
                    WriteLine($"SetDepthStencilState({ObjectValue(assignment)}, {stencilReference});");
                    break;
                case "RasterizerState":
                case "VertexShader":
                case "PixelShader":
                case "GeometryShader":
                case "HullShader":
                case "DomainShader":
                case "ComputeShader":
                    WriteLine($"Set{state.Name}({ObjectValue(assignment)});");
                    break;
                default:
                    throw new NotSupportedException($"A pass that sets {state.Name}.");
            }
        }
    }

    // What an object state is set to: NULL, a variable, an element of one, or a
    // shader compiled there and then.
    private string ObjectValue(EffectAssignment assignment)
    {
        return assignment.Kind switch
        {
            EffectAssignmentKind.Constant => "NULL",
            EffectAssignmentKind.Variable => assignment.VariableName,
            EffectAssignmentKind.ConstantIndex => $"{assignment.VariableName}[{assignment.ArrayIndex}]",
            EffectAssignmentKind.VariableIndex => $"{assignment.VariableName}[{assignment.IndexVariableName}]",
            EffectAssignmentKind.ExpressionIndex => $"{assignment.VariableName}[{Expression(assignment.Expression)}]",
            EffectAssignmentKind.Expression => Expression(assignment.Expression),
            EffectAssignmentKind.InlineShader5 => ShaderExpression(assignment.Shader, true),
            _ => ShaderExpression(assignment.Shader, false),
        };
    }

    private string StateValue(EffectState state, EffectAssignment assignment)
    {
        if (assignment.Kind != EffectAssignmentKind.Constant || state.ValueType == EffectStateValueType.Object)
        {
            return ObjectValue(assignment);
        }

        string[] values = [.. assignment.Constants.Select(constant => ConstantValue(state, constant))];
        return values.Length == 1 ? values[0] : $"float{values.Length}({string.Join(", ", values)})";
    }

    /// <summary>
    /// A state's value written so that it compiles to the same constant: the effect
    /// keeps the type a literal had - a float, an int, a uint, a bool - and not only
    /// its value. A name is an int, a hexadecimal literal a uint.
    /// </summary>
    private static string ConstantValue(EffectState state, EffectConstant constant)
    {
        switch (constant.Type)
        {
            case EffectScalarType.Float:
                return FloatLiteral(BitConverter.UInt32BitsToSingle(constant.Bits), withPoint: true);
            case EffectScalarType.Bool:
                return constant.Bits != 0 ? "true" : "false";
            case EffectScalarType.UInt:
                return $"0x{constant.Bits:X}";
            default:
                if (state.ValueNames != null && state.ValueNames.TryGetValue(constant.Bits, out string name))
                {
                    return name;
                }
                return ((int)constant.Bits).ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// The HLSL that compiles to a shader object: CompileShader, wrapped in
    /// ConstructGSWithSO where the effect gave it stream output - with one
    /// declaration in the fx_4_0 form, with four and the rasterized stream in the
    /// fx_5_0 one.
    /// </summary>
    private string ShaderExpression(EffectShader shader, bool isShader5)
    {
        if (shader.Bytecode == null)
        {
            return "NULL";
        }
        string compiled = $"CompileShader({ReadShader(shader).Profile}, {_functionNames[shader]}())";
        if (!shader.HasStreamOut)
        {
            return compiled;
        }
        if (!isShader5)
        {
            return $"ConstructGSWithSO({compiled}, {Quoted(shader.StreamOutDeclarations[0])})";
        }
        IEnumerable<string> declarations = Enumerable.Range(0, 4).Select(i =>
            i < shader.StreamOutDeclarations.Count && shader.StreamOutDeclarations[i] != null
                ? Quoted(shader.StreamOutDeclarations[i])
                : "NULL");
        return $"ConstructGSWithSO({compiled}, {string.Join(", ", declarations)}, {shader.RasterizedStream})";
    }

    private static bool IsShader5(EffectObjectType type)
    {
        return type is >= EffectObjectType.PixelShader5 and <= EffectObjectType.DomainShader5;
    }

    private static string Expression(byte[] code)
    {
        return PreshaderExpression.Write(Preshader.ReadExpression(code));
    }

    private static string TypeName(EffectType type)
    {
        // A matrix is column major unless it was declared otherwise.
        return type.Class == EffectVariableClass.Numeric && type.Layout == EffectNumericLayout.Matrix && !type.IsColumnMajor
            ? "row_major " + type.Name
            : type.Name;
    }

    private static string ArraySuffix(EffectType type)
    {
        return type.Elements > 0 ? $"[{type.Elements}]" : "";
    }

    private static string Semantic(string semantic)
    {
        return semantic != null ? $" : {semantic}" : "";
    }

    private static string Annotations(IReadOnlyList<EffectAnnotation> annotations)
    {
        if (annotations.Count == 0)
        {
            return "";
        }
        IEnumerable<string> declarations = annotations.Select(annotation =>
        {
            string value;
            string typeName;
            if (annotation.Value == null)
            {
                typeName = "string";
                value = annotation.Type.Elements == 0
                    ? Quoted(annotation.Strings[0])
                    : $"{{ {string.Join(", ", annotation.Strings.Select(Quoted))} }}";
            }
            else
            {
                typeName = TypeName(annotation.Type);
                value = Value(annotation.Type, annotation.Value);
            }
            return $"{typeName} {annotation.Name}{ArraySuffix(annotation.Type)} = {value};";
        });
        return $" <{string.Join(" ", declarations)}>";
    }

    /// <summary>
    /// An initializer, from the value as the effect packs it: every component in
    /// order, a matrix's by row, without the padding the registers would have.
    /// </summary>
    private static string Value(EffectType type, byte[] packed)
    {
        int position = 0;
        return Value(type, packed, ref position, withElements: true);
    }

    private static string Value(EffectType type, byte[] packed, ref int position, bool withElements)
    {
        if (withElements && type.Elements > 0)
        {
            var elements = new List<string>();
            for (int i = 0; i < type.Elements; i++)
            {
                elements.Add(Value(type, packed, ref position, withElements: false));
            }
            return $"{{ {string.Join(", ", elements)} }}";
        }
        if (type.Class == EffectVariableClass.Struct)
        {
            var members = new List<string>();
            foreach (EffectMember member in type.Members)
            {
                members.Add(Value(member.Type, packed, ref position, withElements: true));
            }
            return $"{{ {string.Join(", ", members)} }}";
        }

        int count = type.Rows * type.Columns;
        var components = new List<string>();
        for (int i = 0; i < count; i++)
        {
            uint bits = BitConverter.ToUInt32(packed, position);
            position += 4;
            components.Add(type.ScalarType switch
            {
                EffectScalarType.Float => FloatLiteral(BitConverter.UInt32BitsToSingle(bits), withPoint: false),
                EffectScalarType.Bool => bits != 0 ? "true" : "false",
                EffectScalarType.UInt => bits.ToString(CultureInfo.InvariantCulture),
                _ => ((int)bits).ToString(CultureInfo.InvariantCulture),
            });
        }
        return count == 1 ? components[0] : $"{type.Name}({string.Join(", ", components)})";
    }

    private static string FloatLiteral(float value, bool withPoint)
    {
        string text = value.ToString("R", CultureInfo.InvariantCulture);
        if (withPoint && !text.Contains('.') && !text.Contains('E'))
        {
            text += ".0";
        }
        return text;
    }

    private static string Quoted(string value)
    {
        var text = new StringBuilder("\"");
        foreach (char c in value ?? "")
        {
            text.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\t' => "\\t",
                _ => c.ToString(),
            });
        }
        return text.Append('"').ToString();
    }

    private bool _separate;
    private bool _anythingWritten;

    // A blank line before whatever is written next, if anything is, and only one
    // however many blocks ask for it.
    private void Separate()
    {
        _separate = _anythingWritten;
    }

    private void WriteLine(string line = "")
    {
        if (_separate)
        {
            _writer.WriteLine();
            _separate = false;
        }
        _writer.WriteLine(line.Length == 0 ? "" : _indent + line);
        _anythingWritten = true;
    }
}
