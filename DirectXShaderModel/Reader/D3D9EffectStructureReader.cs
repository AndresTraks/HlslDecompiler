using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// The structure of an fx_2_0 effect. After the tag comes the offset of the
/// structured data, and every offset after that counts from there: the
/// parameters' types and values, the names, and then the structured data itself
/// - counts, the parameters, the techniques with their passes and states - and
/// after it the objects' data: the strings and the shaders of the parameters,
/// then what the states set that a value cannot hold - a shader compiled in the
/// pass, an expression, a parameter named.
/// </summary>
internal sealed class D3D9EffectStructureReader
{
    private readonly byte[] _data;
    private int _position;
    private D3D9EffectObject[] _objects;

    private D3D9EffectStructureReader(byte[] data)
    {
        _data = data;
    }

    public static D3D9Effect Read(byte[] file)
    {
        int structuredOffset = (int)BitConverter.ToUInt32(file, 4);
        var reader = new D3D9EffectStructureReader(file[8..]) { _position = structuredOffset };
        return reader.Read();
    }

    private D3D9Effect Read()
    {
        int parameterCount = (int)ReadUInt32();
        int techniqueCount = (int)ReadUInt32();
        ReadUInt32(); // shader slots
        int objectCount = (int)ReadUInt32();
        _objects = [.. Enumerable.Range(0, objectCount).Select(id => new D3D9EffectObject { Id = id })];

        var parameters = new List<D3D9EffectValue>();
        for (int i = 0; i < parameterCount; i++)
        {
            int typeOffset = (int)ReadUInt32();
            int valueOffset = (int)ReadUInt32();
            uint flags = ReadUInt32();
            int annotationCount = (int)ReadUInt32();
            IReadOnlyList<D3D9EffectValue> annotations = ReadAnnotations(annotationCount);
            parameters.Add(ReadValue(typeOffset, valueOffset, flags, annotations));
        }

        var techniques = new List<D3D9EffectTechnique>();
        for (int i = 0; i < techniqueCount; i++)
        {
            string name = ReadName((int)ReadUInt32());
            int annotationCount = (int)ReadUInt32();
            int passCount = (int)ReadUInt32();
            IReadOnlyList<D3D9EffectValue> annotations = ReadAnnotations(annotationCount);
            var passes = new List<D3D9EffectPass>();
            for (int p = 0; p < passCount; p++)
            {
                string passName = ReadName((int)ReadUInt32());
                int passAnnotationCount = (int)ReadUInt32();
                int stateCount = (int)ReadUInt32();
                IReadOnlyList<D3D9EffectValue> passAnnotations = ReadAnnotations(passAnnotationCount);
                passes.Add(new D3D9EffectPass(passName, passAnnotations, ReadStates(stateCount)));
            }
            techniques.Add(new D3D9EffectTechnique(name, annotations, passes));
        }

        // The objects given data up front - strings, and the shaders of shader
        // parameters - then what the states set that their values do not hold.
        int stringCount = (int)ReadUInt32();
        int resourceCount = (int)ReadUInt32();
        for (int i = 0; i < stringCount; i++)
        {
            int id = (int)ReadUInt32();
            _objects[id].Data = ReadData();
        }
        for (int i = 0; i < resourceCount; i++)
        {
            ReadResource(parameters, techniques);
        }

        return new D3D9Effect { Parameters = parameters, Techniques = techniques };
    }

    private void ReadResource(List<D3D9EffectValue> parameters, List<D3D9EffectTechnique> techniques)
    {
        uint techniqueIndex = ReadUInt32();
        int index = (int)ReadUInt32();
        uint elementIndex = ReadUInt32();
        int stateIndex = (int)ReadUInt32();
        uint usage = ReadUInt32();

        D3D9EffectState state;
        if (techniqueIndex == 0xFFFFFFFF)
        {
            // A sampler's state: the parameter, the element of it, the state.
            D3D9EffectValue sampler = parameters[index];
            int element = elementIndex == 0xFFFFFFFF ? 0 : (int)elementIndex;
            state = (D3D9EffectState)sampler.SamplerStates[element][stateIndex];
        }
        else
        {
            state = (D3D9EffectState)techniques[(int)techniqueIndex].Passes[index].States[stateIndex];
        }

        byte[] data = ReadData();
        switch (usage)
        {
            case 0:
                if (state.Value.IsShader)
                {
                    state.Kind = D3D9StateKind.Shader;
                    state.Shader = data.Length == 0 ? null : data;
                }
                else
                {
                    state.Kind = D3D9StateKind.Expression;
                    state.Expression = data;
                }
                break;
            case 1:
                state.Kind = D3D9StateKind.Parameter;
                state.ParameterName = NullTerminated(data, 0, data.Length);
                break;
            case 2:
                {
                    // The array's name, then the program that picks the element.
                    int nameSize = (int)BitConverter.ToUInt32(data, 0);
                    state.Kind = D3D9StateKind.ArraySelector;
                    state.ParameterName = NullTerminated(data, 4, nameSize);
                    state.Expression = data[(4 + nameSize)..];
                    break;
                }
            default:
                throw new InvalidDataException($"A state resource of usage {usage}.");
        }
    }

    private IReadOnlyList<D3D9EffectValue> ReadAnnotations(int count)
    {
        var annotations = new List<D3D9EffectValue>();
        for (int i = 0; i < count; i++)
        {
            int typeOffset = (int)ReadUInt32();
            int valueOffset = (int)ReadUInt32();
            annotations.Add(ReadValue(typeOffset, valueOffset, 0, []));
        }
        return annotations;
    }

    private IReadOnlyList<D3D9EffectState> ReadStates(int count)
    {
        var states = new List<D3D9EffectState>();
        for (int i = 0; i < count; i++)
        {
            int operation = (int)ReadUInt32();
            int index = (int)ReadUInt32();
            int typeOffset = (int)ReadUInt32();
            int valueOffset = (int)ReadUInt32();
            states.Add(new D3D9EffectState
            {
                Operation = operation,
                Index = index,
                Value = ReadValue(typeOffset, valueOffset, 0, []),
                Kind = D3D9StateKind.Constant,
            });
        }
        return states;
    }

    /// <summary>A type at one offset and its value at another.</summary>
    private D3D9EffectValue ReadValue(int typeOffset, int valueOffset, uint flags, IReadOnlyList<D3D9EffectValue> annotations)
    {
        int typePosition = typeOffset;
        D3D9EffectValue type = ReadType(ref typePosition);
        int elements = Math.Max(type.Elements, 1);

        byte[] data = null;
        var objects = new List<D3D9EffectObject>();
        var samplerStates = new List<IReadOnlyList<D3D9EffectState>>();
        if (type.Class != D3DXParameterClass.Object)
        {
            int size = type.ElementSize * elements;
            data = _data[valueOffset..(valueOffset + size)];
        }
        else if (type.IsSampler)
        {
            // A sampler's states are its value, each element's in turn.
            int saved = _position;
            _position = valueOffset;
            for (int i = 0; i < elements; i++)
            {
                samplerStates.Add(ReadStates((int)ReadUInt32()));
            }
            _position = saved;
        }
        else
        {
            for (int i = 0; i < elements; i++)
            {
                objects.Add(_objects[(int)BitConverter.ToUInt32(_data, valueOffset + i * 4)]);
            }
        }

        return new D3D9EffectValue
        {
            Name = type.Name,
            Semantic = type.Semantic,
            Type = type.Type,
            Class = type.Class,
            Elements = type.Elements,
            Rows = type.Rows,
            Columns = type.Columns,
            Members = type.Members,
            Flags = flags,
            Annotations = annotations,
            Data = data,
            Objects = objects,
            SamplerStates = samplerStates,
        };
    }

    // A type: its D3DX type and class, name, semantic and array length, then its
    // dimensions, or for a struct its members' types, which follow it.
    private D3D9EffectValue ReadType(ref int position)
    {
        var type = (D3DXParameterType)ReadUInt32At(ref position);
        var typeClass = (D3DXParameterClass)ReadUInt32At(ref position);
        string name = ReadName((int)ReadUInt32At(ref position));
        string semantic = ReadName((int)ReadUInt32At(ref position));
        int elements = (int)ReadUInt32At(ref position);
        int rows = 0;
        int columns = 0;
        var members = new List<D3D9EffectValue>();
        switch (typeClass)
        {
            case D3DXParameterClass.Vector:
                columns = (int)ReadUInt32At(ref position);
                rows = (int)ReadUInt32At(ref position);
                break;
            case D3DXParameterClass.Scalar:
            case D3DXParameterClass.MatrixRows:
            case D3DXParameterClass.MatrixColumns:
                rows = (int)ReadUInt32At(ref position);
                columns = (int)ReadUInt32At(ref position);
                break;
            case D3DXParameterClass.Struct:
                int memberCount = (int)ReadUInt32At(ref position);
                for (int i = 0; i < memberCount; i++)
                {
                    members.Add(ReadType(ref position));
                }
                break;
        }
        return new D3D9EffectValue
        {
            Name = name,
            Semantic = semantic,
            Type = type,
            Class = typeClass,
            Elements = elements,
            Rows = rows,
            Columns = columns,
            Members = members,
        };
    }

    // A name is its length, terminator included, then its characters.
    private string ReadName(int offset)
    {
        int size = (int)BitConverter.ToUInt32(_data, offset);
        return size == 0 ? null : NullTerminated(_data, offset + 4, size);
    }

    private static string NullTerminated(byte[] bytes, int start, int size)
    {
        int end = Array.IndexOf(bytes, (byte)0, start, size);
        return Encoding.ASCII.GetString(bytes, start, (end < 0 ? start + size : end) - start);
    }

    // An object's data is its length and then its bytes, padded to a dword.
    private byte[] ReadData()
    {
        int size = (int)ReadUInt32();
        byte[] data = _data[_position..(_position + size)];
        _position += (size + 3) & ~3;
        return data;
    }

    private uint ReadUInt32()
    {
        return ReadUInt32At(ref _position);
    }

    private uint ReadUInt32At(ref int position)
    {
        uint value = BitConverter.ToUInt32(_data, position);
        position += 4;
        return value;
    }
}
