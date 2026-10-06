using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// The structured part of an fx_4_x or fx_5_0 effect body, read in the order the
/// runtime loads it: the constant buffers with their variables, the object
/// variables, then the groups - or for fx_4_x, which has none, the techniques - with
/// their passes. Each record names what it is by offsets into the unstructured
/// data that comes before it, where the strings, types, values and shaders are.
/// </summary>
internal sealed class EffectStructureReader
{
    private readonly byte[] _body;
    private readonly uint _tag;
    private readonly int _unstructuredStart;
    private int _position;
    private readonly Dictionary<int, EffectType> _types = [];

    private EffectStructureReader(byte[] body)
    {
        _body = body;
        _tag = ReadUInt32At(0);
        _unstructuredStart = IsFx5 ? 96 : 76;
    }

    private bool IsFx5 => _tag == EffectReader.Fx50;

    public static Effect Read(byte[] body)
    {
        return new EffectStructureReader(body).Read();
    }

    private Effect Read()
    {
        // The header's counts: the effect's buffers, numeric and object variables,
        // the pool's three, the techniques, the size of the unstructured data, and
        // tallies of what the runtime allocates. fx_5_0 adds groups and interfaces.
        int bufferCount = (int)ReadUInt32At(4);
        int objectVariableCount = (int)ReadUInt32At(12);
        int poolCounts = (int)(ReadUInt32At(16) | ReadUInt32At(20) | ReadUInt32At(24));
        int techniqueCount = (int)ReadUInt32At(28);
        int unstructuredSize = (int)ReadUInt32At(32);
        int groupCount = IsFx5 ? (int)ReadUInt32At(76) : 0;
        int interfaceVariableCount = IsFx5 ? (int)ReadUInt32At(84) : 0;
        if (poolCounts != 0)
        {
            throw new NotSupportedException("An effect sharing variables through an effect pool.");
        }

        _position = _unstructuredStart + unstructuredSize;

        var buffers = new List<EffectConstantBuffer>();
        for (int i = 0; i < bufferCount; i++)
        {
            buffers.Add(ReadConstantBuffer());
        }

        var objects = new List<EffectObjectVariable>();
        for (int i = 0; i < objectVariableCount; i++)
        {
            objects.Add(ReadObjectVariable());
        }

        if (interfaceVariableCount != 0)
        {
            throw new NotSupportedException("An effect with interface variables.");
        }

        var groups = new List<EffectGroup>();
        if (IsFx5)
        {
            for (int i = 0; i < groupCount; i++)
            {
                string name = ReadString(ReadUInt32());
                int techniques = (int)ReadUInt32();
                IReadOnlyList<EffectAnnotation> annotations = ReadAnnotations();
                groups.Add(new EffectGroup(name, annotations,
                    [.. Enumerable.Range(0, techniques).Select(_ => ReadTechnique())]));
            }
        }
        else
        {
            groups.Add(new EffectGroup(null, [],
                [.. Enumerable.Range(0, techniqueCount).Select(_ => ReadTechnique())]));
        }

        if (_position != _body.Length)
        {
            throw new InvalidDataException(
                $"The effect's structured data ends at {_position}, not at the end of the effect at {_body.Length}.");
        }

        return new Effect
        {
            Tag = _tag,
            ConstantBuffers = buffers,
            ObjectVariables = objects,
            Groups = groups,
        };
    }

    private EffectConstantBuffer ReadConstantBuffer()
    {
        string name = ReadString(ReadUInt32());
        int size = (int)ReadUInt32();
        uint flags = ReadUInt32();
        int variableCount = (int)ReadUInt32();
        int bindPoint = (int)ReadUInt32();
        IReadOnlyList<EffectAnnotation> annotations = ReadAnnotations();

        var variables = new List<EffectNumericVariable>();
        for (int i = 0; i < variableCount; i++)
        {
            string variableName = ReadString(ReadUInt32());
            EffectType type = ReadType(ReadUInt32());
            string semantic = ReadString(ReadUInt32());
            int offset = (int)ReadUInt32();
            uint defaultValue = ReadUInt32();
            uint variableFlags = ReadUInt32();
            variables.Add(new EffectNumericVariable
            {
                Name = variableName,
                Type = type,
                Semantic = semantic,
                Offset = offset,
                DefaultValue = defaultValue == 0 ? null : ReadUnstructuredBytes(defaultValue, type.PackedSize),
                HasExplicitBindPoint = (variableFlags & 4) != 0,
                Annotations = ReadAnnotations(),
            });
        }

        return new EffectConstantBuffer
        {
            Name = name,
            Size = size,
            IsTextureBuffer = (flags & 1) != 0,
            ExplicitBindPoint = bindPoint,
            Annotations = annotations,
            Variables = variables,
        };
    }

    private EffectObjectVariable ReadObjectVariable()
    {
        string name = ReadString(ReadUInt32());
        EffectType type = ReadType(ReadUInt32());
        string semantic = ReadString(ReadUInt32());
        int bindPoint = (int)ReadUInt32();
        int elements = Math.Max(type.Elements, 1);

        var blocks = new List<IReadOnlyList<EffectAssignment>>();
        var shaders = new List<EffectShader>();
        var strings = new List<string>();
        if (type.Class != EffectVariableClass.Object)
        {
            throw new InvalidDataException($"The object variable {name} is of a {type.Class} type.");
        }
        if (type.IsStateBlock)
        {
            for (int i = 0; i < elements; i++)
            {
                blocks.Add(ReadAssignments((int)ReadUInt32()));
            }
        }
        else if (type.IsShader)
        {
            for (int i = 0; i < elements; i++)
            {
                shaders.Add(type.ObjectType switch
                {
                    EffectObjectType.VertexShader or EffectObjectType.PixelShader or EffectObjectType.GeometryShader
                        => ReadShader(ReadUInt32()),
                    EffectObjectType.GeometryShaderSO => ReadStreamOutShader(ReadUInt32(), ReadUInt32()),
                    _ => ReadShader5(),
                });
            }
        }
        else if (type.ObjectType == EffectObjectType.String)
        {
            for (int i = 0; i < elements; i++)
            {
                strings.Add(ReadString(ReadUInt32()));
            }
        }

        return new EffectObjectVariable
        {
            Name = name,
            Type = type,
            Semantic = semantic,
            ExplicitBindPoint = bindPoint,
            Blocks = blocks,
            Shaders = shaders,
            Strings = strings,
            Annotations = ReadAnnotations(),
        };
    }

    private EffectTechnique ReadTechnique()
    {
        string name = ReadString(ReadUInt32());
        int passCount = (int)ReadUInt32();
        IReadOnlyList<EffectAnnotation> annotations = ReadAnnotations();
        var passes = new List<EffectPass>();
        for (int i = 0; i < passCount; i++)
        {
            string passName = ReadString(ReadUInt32());
            int assignmentCount = (int)ReadUInt32();
            IReadOnlyList<EffectAnnotation> passAnnotations = ReadAnnotations();
            passes.Add(new EffectPass(passName, passAnnotations, ReadAssignments(assignmentCount)));
        }
        return new EffectTechnique(name, annotations, passes);
    }

    private IReadOnlyList<EffectAnnotation> ReadAnnotations()
    {
        int count = (int)ReadUInt32();
        var annotations = new List<EffectAnnotation>();
        for (int i = 0; i < count; i++)
        {
            string name = ReadString(ReadUInt32());
            EffectType type = ReadType(ReadUInt32());
            if (type.Class == EffectVariableClass.Object && type.ObjectType == EffectObjectType.String)
            {
                // A string annotation's offsets are here, one per element.
                string[] strings = [.. Enumerable.Range(0, Math.Max(type.Elements, 1))
                    .Select(_ => ReadString(ReadUInt32()))];
                annotations.Add(new EffectAnnotation(name, type, strings, null));
            }
            else
            {
                annotations.Add(new EffectAnnotation(name, type, [],
                    ReadUnstructuredBytes(ReadUInt32(), type.PackedSize)));
            }
        }
        return annotations;
    }

    private IReadOnlyList<EffectAssignment> ReadAssignments(int count)
    {
        var assignments = new List<EffectAssignment>();
        for (int i = 0; i < count; i++)
        {
            int state = (int)ReadUInt32();
            int index = (int)ReadUInt32();
            var kind = (EffectAssignmentKind)ReadUInt32();
            uint initializer = ReadUInt32();
            assignments.Add(kind switch
            {
                EffectAssignmentKind.Constant => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    Constants = [.. Enumerable.Range(0, (int)ReadUnstructuredUInt32(initializer))
                        .Select(c => new EffectConstant(
                            (EffectScalarType)ReadUnstructuredUInt32(initializer + 4 + (uint)c * 8),
                            ReadUnstructuredUInt32(initializer + 8 + (uint)c * 8)))],
                },
                EffectAssignmentKind.Variable => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    VariableName = ReadString(initializer),
                },
                EffectAssignmentKind.ConstantIndex => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    VariableName = ReadString(ReadUnstructuredUInt32(initializer)),
                    ArrayIndex = (int)ReadUnstructuredUInt32(initializer + 4),
                },
                EffectAssignmentKind.VariableIndex => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    VariableName = ReadString(ReadUnstructuredUInt32(initializer)),
                    IndexVariableName = ReadString(ReadUnstructuredUInt32(initializer + 4)),
                },
                EffectAssignmentKind.ExpressionIndex => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    VariableName = ReadString(ReadUnstructuredUInt32(initializer)),
                    Expression = ReadBlock(ReadUnstructuredUInt32(initializer + 4)),
                },
                EffectAssignmentKind.Expression => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    Expression = ReadBlock(initializer),
                },
                EffectAssignmentKind.InlineShader => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    Shader = ReadStreamOutShader(
                        ReadUnstructuredUInt32(initializer), ReadUnstructuredUInt32(initializer + 4)),
                },
                EffectAssignmentKind.InlineShader5 => new EffectAssignment
                {
                    State = state,
                    Index = index,
                    Kind = kind,
                    Shader = ReadShader5At(initializer),
                },
                _ => throw new InvalidDataException($"Assignment kind {kind}."),
            });
        }
        return assignments;
    }

    private EffectShader ReadShader(uint offset)
    {
        byte[] bytecode = ReadBlock(offset);
        return new EffectShader { Bytecode = bytecode.Length == 0 ? null : bytecode };
    }

    // An inline shader in fx_4_x is a shader and a stream output declaration, which
    // a shader without stream output has an empty offset for.
    private EffectShader ReadStreamOutShader(uint shaderOffset, uint declarationOffset)
    {
        byte[] bytecode = ReadBlock(shaderOffset);
        string declaration = ReadString(declarationOffset);
        return new EffectShader
        {
            Bytecode = bytecode.Length == 0 ? null : bytecode,
            HasStreamOut = declaration != null,
            StreamOutDeclarations = declaration == null ? [] : [declaration],
        };
    }

    private EffectShader ReadShader5()
    {
        EffectShader shader = ReadShader5At((uint)(_position - _unstructuredStart), structured: true);
        _position += 36;
        return shader;
    }

    // SBinaryShaderData5: the shader, four stream output declarations and how many
    // of them there are, the stream that is rasterized, and interface bindings.
    private EffectShader ReadShader5At(uint offset, bool structured = false)
    {
        uint Field(int index) => structured
            ? ReadUInt32At(_position + index * 4)
            : ReadUnstructuredUInt32(offset + (uint)index * 4);

        byte[] bytecode = ReadBlock(Field(0));
        int declarationCount = (int)Field(5);
        if (Field(7) != 0)
        {
            throw new NotSupportedException("A shader with interface bindings.");
        }
        return new EffectShader
        {
            Bytecode = bytecode.Length == 0 ? null : bytecode,
            HasStreamOut = declarationCount != 0,
            StreamOutDeclarations = [.. Enumerable.Range(0, declarationCount).Select(i => ReadString(Field(1 + i)))],
            RasterizedStream = (int)Field(6),
        };
    }

    private EffectType ReadType(uint offset)
    {
        if (_types.TryGetValue((int)offset, out EffectType cached))
        {
            return cached;
        }

        string name = ReadString(ReadUnstructuredUInt32(offset));
        var typeClass = (EffectVariableClass)ReadUnstructuredUInt32(offset + 4);
        int elements = (int)ReadUnstructuredUInt32(offset + 8);
        int totalSize = (int)ReadUnstructuredUInt32(offset + 12);
        int stride = (int)ReadUnstructuredUInt32(offset + 16);
        int packedSize = (int)ReadUnstructuredUInt32(offset + 20);
        uint data = ReadUnstructuredUInt32(offset + 24);

        EffectType type = typeClass switch
        {
            EffectVariableClass.Numeric => new EffectType
            {
                Name = name,
                Class = typeClass,
                Elements = elements,
                TotalSize = totalSize,
                Stride = stride,
                PackedSize = packedSize,
                // SBinaryNumericType: a bit field of layout, scalar type, rows,
                // columns and whether a matrix is stored by column.
                Layout = (EffectNumericLayout)(data & 7),
                ScalarType = (EffectScalarType)((data >> 3) & 0x1F),
                Rows = (int)((data >> 8) & 7),
                Columns = (int)((data >> 11) & 7),
                IsColumnMajor = ((data >> 14) & 1) != 0,
            },
            EffectVariableClass.Object => new EffectType
            {
                Name = name,
                Class = typeClass,
                Elements = elements,
                TotalSize = totalSize,
                Stride = stride,
                PackedSize = packedSize,
                ObjectType = (EffectObjectType)data,
            },
            EffectVariableClass.Struct => new EffectType
            {
                Name = name,
                Class = typeClass,
                Elements = elements,
                TotalSize = totalSize,
                Stride = stride,
                PackedSize = packedSize,
                Members = [.. Enumerable.Range(0, (int)data).Select(m =>
                {
                    uint member = offset + 28 + (uint)m * 16;
                    return new EffectMember(
                        ReadString(ReadUnstructuredUInt32(member)),
                        ReadString(ReadUnstructuredUInt32(member + 4)),
                        (int)ReadUnstructuredUInt32(member + 8),
                        ReadType(ReadUnstructuredUInt32(member + 12)));
                })],
            },
            _ => throw new NotSupportedException($"An effect variable of {typeClass} type."),
        };
        _types[(int)offset] = type;
        return type;
    }

    // A block in the unstructured data is its length and then its bytes.
    private byte[] ReadBlock(uint offset)
    {
        int size = (int)ReadUnstructuredUInt32(offset);
        return ReadUnstructuredBytes(offset + 4, size);
    }

    private string ReadString(uint offset)
    {
        if (offset == 0)
        {
            return null;
        }
        int start = _unstructuredStart + (int)offset;
        int end = Array.IndexOf(_body, (byte)0, start);
        return Encoding.ASCII.GetString(_body, start, end - start);
    }

    private byte[] ReadUnstructuredBytes(uint offset, int count)
    {
        int start = _unstructuredStart + (int)offset;
        return _body[start..(start + count)];
    }

    private uint ReadUnstructuredUInt32(uint offset)
    {
        return ReadUInt32At(_unstructuredStart + (int)offset);
    }

    private uint ReadUInt32()
    {
        uint value = ReadUInt32At(_position);
        _position += 4;
        return value;
    }

    private uint ReadUInt32At(int position)
    {
        return BitConverter.ToUInt32(_body, position);
    }
}
