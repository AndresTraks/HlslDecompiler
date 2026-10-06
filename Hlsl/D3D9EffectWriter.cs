using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// An fx_2_0 effect written back as the .fx it was compiled from: its parameters,
/// textures and samplers, a function for each of its shaders, and the techniques
/// and passes with the states they set.
///
/// Laid out the way <see cref="EffectWriter"/> lays out a Direct3D 10 effect: the
/// parameters in the effect's order, each shader's function just before the first
/// parameter that compiles it, and the functions only a pass compiles after the
/// parameters.
/// </summary>
public sealed class D3D9EffectWriter
{
    private readonly D3D9Effect _effect;
    private readonly bool _doAstAnalysis;

    private readonly Dictionary<string, string> _functionsByBytecode = [];
    private readonly Dictionary<string, string> _functionBodies = [];
    private readonly HashSet<string> _written = [];
    private readonly Dictionary<string, ShaderTypeInfo> _constantTypes = [];
    private readonly Dictionary<D3D9EffectValue, string> _structTypeNames = [];

    private TextWriter _writer;
    private string _indent = "";
    private bool _separate;
    private bool _anythingWritten;

    public D3D9EffectWriter(D3D9Effect effect, bool doAstAnalysis)
    {
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

        foreach (D3D9EffectValue parameter in _effect.Parameters)
        {
            WriteStructType(parameter);
        }
        foreach (D3D9EffectValue parameter in _effect.Parameters)
        {
            if (parameter.IsShader)
            {
                foreach (D3D9EffectObject shader in parameter.Objects)
                {
                    WriteFunction(shader.Data);
                }
            }
            WriteParameter(parameter);
        }
        foreach (string name in _functionBodies.Keys.Where(name => !_written.Contains(name)).ToList())
        {
            WriteFunction(name);
        }
        foreach (D3D9EffectTechnique technique in _effect.Techniques)
        {
            WriteTechnique(technique);
        }
    }

    /// <summary>
    /// A function for every shader, named after where it is set: the parameter that
    /// holds it, or the pass that compiles it. Two that are the same bytecode are
    /// one function.
    /// </summary>
    private void NameShaders()
    {
        foreach (D3D9EffectValue parameter in _effect.Parameters.Where(p => p.IsShader))
        {
            for (int i = 0; i < parameter.Objects.Count; i++)
            {
                string suffix = parameter.Elements > 0 ? i.ToString(CultureInfo.InvariantCulture) : "";
                NameShader(parameter.Objects[i].Data, parameter.Name + suffix);
            }
        }
        foreach (D3D9EffectTechnique technique in _effect.Techniques)
        {
            foreach (D3D9EffectPass pass in technique.Passes)
            {
                foreach (D3D9EffectState state in pass.States.Where(s => s.Kind == D3D9StateKind.Shader))
                {
                    NameShader(state.Shader, $"{technique.Name}_{pass.Name}");
                }
            }
        }
    }

    private void NameShader(byte[] bytecode, string place)
    {
        if (bytecode == null || bytecode.Length == 0)
        {
            return;
        }
        string key = Convert.ToBase64String(bytecode);
        if (_functionsByBytecode.ContainsKey(key))
        {
            return;
        }

        ShaderModel model = ReadShader(bytecode);
        string name = $"{model.Stage}_{place}";
        while (_functionBodies.ContainsKey(name))
        {
            name += "_";
        }
        HlslWriter writer = _doAstAnalysis
            ? new HlslAstWriter(model) { FunctionName = name }
            : new HlslSimpleWriter(model) { FunctionName = name };
        var body = new StringWriter();
        writer.Write(body);
        foreach ((string constant, ShaderTypeInfo type) in writer.ConstantTypes)
        {
            _constantTypes.TryAdd(constant, type);
        }
        _functionsByBytecode[key] = name;
        _functionBodies[name] = body.ToString();
    }

    private static ShaderModel ReadShader(byte[] bytecode)
    {
        using var reader = new ShaderReader(new MemoryStream(bytecode));
        return reader.ReadShader();
    }

    private string FunctionName(byte[] bytecode)
    {
        return _functionsByBytecode[Convert.ToBase64String(bytecode)];
    }

    private void WriteFunction(byte[] bytecode)
    {
        if (bytecode != null && bytecode.Length != 0)
        {
            WriteFunction(FunctionName(bytecode));
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

    // An fx_2_0 effect keeps a struct's members but not the name of its type, so
    // the type is named after the first parameter of it.
    private void WriteStructType(D3D9EffectValue value)
    {
        if (value.Class != D3DXParameterClass.Struct || _structTypeNames.ContainsKey(value))
        {
            return;
        }
        foreach (D3D9EffectValue member in value.Members)
        {
            WriteStructType(member);
        }
        string name = $"{value.Name}_type";
        _structTypeNames[value] = name;
        WriteLine($"struct {name}");
        WriteLine("{");
        _indent = "\t";
        foreach (D3D9EffectValue member in value.Members)
        {
            WriteLine($"{TypeName(member)} {member.Name}{ArraySuffix(member)}{Semantic(member.Semantic)};");
        }
        _indent = "";
        WriteLine("};");
        Separate();
    }

    private void WriteParameter(D3D9EffectValue parameter)
    {
        // Shared between the effects of a pool.
        string storage = (parameter.Flags & 1) != 0 ? "shared " : "";
        string declaration = $"{storage}{TypeName(parameter)} {parameter.Name}{ArraySuffix(parameter)}"
            + Semantic(parameter.Semantic) + Annotations(parameter.Annotations);

        if (parameter.IsSampler)
        {
            IEnumerable<string> samplers = parameter.SamplerStates.Select(SamplerState);
            if (parameter.Elements == 0)
            {
                Separate();
                WriteLine($"{declaration} = {samplers.Single()};");
                Separate();
            }
            else
            {
                Separate();
                WriteLine($"{declaration} = {{ {string.Join(", ", samplers)} }};");
                Separate();
            }
        }
        else if (parameter.IsShader)
        {
            IEnumerable<string> shaders = parameter.Objects.Select(o => ShaderExpression(o.Data));
            WriteLine(parameter.Elements == 0
                ? $"{declaration} = {shaders.Single()};"
                : $"{declaration} = {{ {string.Join(", ", shaders)} }};");
        }
        else if (parameter.Type == D3DXParameterType.String)
        {
            IEnumerable<string> strings = parameter.Objects.Select(o => Quoted(StringOf(o)));
            WriteLine(parameter.Elements == 0
                ? $"{declaration} = {strings.Single()};"
                : $"{declaration} = {{ {string.Join(", ", strings)} }};");
        }
        else if (parameter.Data != null && parameter.Data.Any(b => b != 0))
        {
            WriteLine($"{declaration} = {Value(parameter)};");
        }
        else
        {
            // A parameter with no initializer has a value of nothing but zeros,
            // which is the same as one initialized to zero.
            WriteLine(declaration + ";");
        }
    }

    private string SamplerState(IReadOnlyList<D3D9EffectState> states)
    {
        var text = new StringBuilder("sampler_state" + Environment.NewLine + _indent + "{" + Environment.NewLine);
        foreach (D3D9EffectState state in states)
        {
            text.Append($"{_indent}\t{StateAssignment(state, inSampler: true)}{Environment.NewLine}");
        }
        return text.Append(_indent + "}").ToString().Replace(Environment.NewLine, "\r\n");
    }

    private void WriteTechnique(D3D9EffectTechnique technique)
    {
        Separate();
        WriteLine($"technique{(technique.Name != null ? " " + technique.Name : "")}{Annotations(technique.Annotations)}");
        WriteLine("{");
        foreach (D3D9EffectPass pass in technique.Passes)
        {
            _indent = "\t";
            WriteLine($"pass{(pass.Name != null ? " " + pass.Name : "")}{Annotations(pass.Annotations)}");
            WriteLine("{");
            _indent = "\t\t";
            foreach (D3D9EffectState state in pass.States)
            {
                WriteLine(StateAssignment(state, inSampler: false));
            }
            _indent = "\t";
            WriteLine("}");
        }
        _indent = "";
        WriteLine("}");
    }

    /// <summary>
    /// One state as an effect file sets it. In a pass a state of several - a
    /// texture stage's, a sampler's, a light's - says which it is; in a sampler the
    /// sampler is the one being declared.
    /// </summary>
    private string StateAssignment(D3D9EffectState state, bool inSampler)
    {
        D3D9EffectStateInfo info = D3D9EffectStates.All[state.Operation];
        string index = info.IsIndexed && !inSampler ? $"[{state.Index}]" : "";
        return $"{info.Name}{index} = {StateValue(state, info)};";
    }

    private string StateValue(D3D9EffectState state, D3D9EffectStateInfo info)
    {
        D3D9EffectValue value = state.Value;
        switch (state.Kind)
        {
            case D3D9StateKind.Shader:
                return ShaderExpression(state.Shader);
            case D3D9StateKind.Parameter:
                return value.IsTexture ? $"<{state.ParameterName}>" : $"({state.ParameterName})";
            case D3D9StateKind.ArraySelector:
                return $"({state.ParameterName}[{PreshaderExpression.Write(Preshader.ReadTokenStream(state.Expression))}])";
            case D3D9StateKind.Expression:
                return $"({ExpressionValue(state)})";
        }

        // An object state set to nothing: NULL, stored as the integer 0.
        if (value.Class == D3DXParameterClass.Object || info.Name is "VertexShader" or "PixelShader" or "Texture" or "Sampler")
        {
            return "NULL";
        }

        if (value.Data.Length == 4)
        {
            uint bits = BitConverter.ToUInt32(value.Data, 0);
            if (info.ValueNames != null && info.ValueNames.TryGetValue(bits, out string name))
            {
                return name;
            }
            return value.Type switch
            {
                D3DXParameterType.Float => FloatLiteral(BitConverter.ToSingle(value.Data, 0)),
                D3DXParameterType.Bool => bits != 0 ? "TRUE" : "FALSE",
                // A colour or a mask reads as one in hexadecimal.
                _ => bits > 0xFFFF ? $"0x{bits:X8}" : ((int)bits).ToString(CultureInfo.InvariantCulture),
            };
        }
        return Value(value);
    }

    /// <summary>
    /// A state set from uniforms, as the expression the effect computes it with -
    /// or, where all it does is copy a parameter into the state, the parameter.
    /// </summary>
    private static string ExpressionValue(D3D9EffectState state)
    {
        Preshader expression = Preshader.ReadTokenStream(state.Expression);
        int components = Math.Max(state.Value.Rows * state.Value.Columns, 1);
        string[] outputs = PreshaderExpression.Write(expression, components);
        if (outputs.Length == 1)
        {
            return outputs[0];
        }
        foreach (D3D9ConstantDeclaration input in expression.Inputs.Declarations)
        {
            string[] whole = [.. Enumerable.Range(0, components)
                .Select(c => PreshaderExpression.InputName(expression.Inputs, input.RegisterIndex * 4 + ComponentOffset(state.Value, c)))];
            if (whole.SequenceEqual(outputs))
            {
                return input.Name;
            }
        }
        return $"{ValueTypeName(state.Value)}({string.Join(", ", outputs)})";
    }

    // Where the c'th component of a value of this shape is in registers: a row of
    // four per register, however few columns it has.
    private static int ComponentOffset(D3D9EffectValue value, int component)
    {
        int columns = Math.Max(value.Columns, 1);
        return (component / columns) * 4 + component % columns;
    }

    private string ShaderExpression(byte[] bytecode)
    {
        if (bytecode == null || bytecode.Length == 0)
        {
            return "NULL";
        }
        return $"compile {ReadShader(bytecode).Profile} {FunctionName(bytecode)}()";
    }

    private string TypeName(D3D9EffectValue value)
    {
        if (value.Class == D3DXParameterClass.Struct)
        {
            return _structTypeNames.TryGetValue(value, out string structName) ? structName : $"{value.Name}_type";
        }
        string name = ValueTypeName(value);
        // Row major where the shaders that read it say so: the effect's own class
        // calls every matrix rows.
        if (value.Class is D3DXParameterClass.MatrixRows or D3DXParameterClass.MatrixColumns
            && _constantTypes.TryGetValue(value.Name, out ShaderTypeInfo read)
            && read.ParameterClass == ParameterClass.MatrixRows)
        {
            return "row_major " + name;
        }
        return name;
    }

    private static string ValueTypeName(D3D9EffectValue value)
    {
        string scalar = value.Type switch
        {
            D3DXParameterType.Bool => "bool",
            D3DXParameterType.Int => "int",
            D3DXParameterType.Float => "float",
            D3DXParameterType.String => "string",
            D3DXParameterType.Texture => "texture",
            D3DXParameterType.Texture1D => "texture1D",
            D3DXParameterType.Texture2D => "texture2D",
            D3DXParameterType.Texture3D => "texture3D",
            D3DXParameterType.TextureCube => "textureCUBE",
            D3DXParameterType.Sampler => "sampler",
            D3DXParameterType.Sampler1D => "sampler1D",
            D3DXParameterType.Sampler2D => "sampler2D",
            D3DXParameterType.Sampler3D => "sampler3D",
            D3DXParameterType.SamplerCube => "samplerCUBE",
            D3DXParameterType.PixelShader => "PixelShader",
            D3DXParameterType.VertexShader => "VertexShader",
            _ => throw new NotSupportedException($"An fx_2_0 parameter of type {value.Type}."),
        };
        return value.Class switch
        {
            D3DXParameterClass.Vector => scalar + value.Columns,
            D3DXParameterClass.MatrixRows or D3DXParameterClass.MatrixColumns => $"{scalar}{value.Rows}x{value.Columns}",
            _ => scalar,
        };
    }

    private static string ArraySuffix(D3D9EffectValue value)
    {
        return value.Elements > 0 ? $"[{value.Elements}]" : "";
    }

    private static string Semantic(string semantic)
    {
        return semantic != null ? $" : {semantic}" : "";
    }

    private string Annotations(IReadOnlyList<D3D9EffectValue> annotations)
    {
        if (annotations.Count == 0)
        {
            return "";
        }
        IEnumerable<string> declarations = annotations.Select(annotation =>
        {
            string value = annotation.Type == D3DXParameterType.String
                ? (annotation.Elements == 0
                    ? Quoted(StringOf(annotation.Objects[0]))
                    : $"{{ {string.Join(", ", annotation.Objects.Select(o => Quoted(StringOf(o))))} }}")
                : Value(annotation);
            return $"{ValueTypeName(annotation)} {annotation.Name}{ArraySuffix(annotation)} = {value};";
        });
        return $" <{string.Join(" ", declarations)}>";
    }

    private static string StringOf(D3D9EffectObject element)
    {
        return element.Data == null ? "" : Encoding.ASCII.GetString(element.Data).TrimEnd('\0');
    }

    /// <summary>
    /// An initializer from a numeric value's data: every element, member and
    /// component in order, a matrix's by row.
    /// </summary>
    private string Value(D3D9EffectValue value)
    {
        int position = 0;
        return Value(value, value.Data, ref position, withElements: true);
    }

    private string Value(D3D9EffectValue value, byte[] data, ref int position, bool withElements)
    {
        if (withElements && value.Elements > 0)
        {
            var elements = new List<string>();
            for (int i = 0; i < value.Elements; i++)
            {
                elements.Add(Value(value, data, ref position, withElements: false));
            }
            return $"{{ {string.Join(", ", elements)} }}";
        }
        if (value.Class == D3DXParameterClass.Struct)
        {
            var members = new List<string>();
            foreach (D3D9EffectValue member in value.Members)
            {
                members.Add(Value(member, data, ref position, withElements: true));
            }
            return $"{{ {string.Join(", ", members)} }}";
        }

        int count = value.Rows * value.Columns;
        var components = new List<string>();
        for (int i = 0; i < count; i++)
        {
            uint bits = BitConverter.ToUInt32(data, position);
            position += 4;
            components.Add(value.Type switch
            {
                D3DXParameterType.Float => FloatLiteral(BitConverter.UInt32BitsToSingle(bits)),
                D3DXParameterType.Bool => bits != 0 ? "true" : "false",
                _ => ((int)bits).ToString(CultureInfo.InvariantCulture),
            });
        }
        return count == 1 ? components[0] : $"{ValueTypeName(value)}({string.Join(", ", components)})";
    }

    private static string FloatLiteral(float value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string Quoted(string value)
    {
        var text = new StringBuilder("\"");
        foreach (char c in value)
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

    // A blank line before whatever is written next, if anything is.
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
