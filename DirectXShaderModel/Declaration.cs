using System;

namespace HlslDecompiler.DirectXShaderModel;

public class ConstantDeclaration
{
    public string Name { get; }
    public short RegisterIndex { get; }
    public ShaderTypeInfo TypeInfo { get; }

    public ConstantDeclaration(string name, short registerIndex, ShaderTypeInfo typeInfo)
    {
        Name = name;
        RegisterIndex = registerIndex;
        TypeInfo = typeInfo;
    }

    public int GetSamplerDimension()
    {
        return TypeInfo.ParameterType switch
        {
            ParameterType.Sampler1D => 1,
            ParameterType.Sampler2D => 2,
            ParameterType.Sampler3D or ParameterType.SamplerCube => 3,
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// Registers per array element. Depends on matrix packing, so subclasses read
    /// it off the declared size.
    /// </summary>
    public virtual int RegistersPerElement => System.Math.Max(TypeInfo.Rows, 1);

    public override string ToString()
    {
        return Name;
    }
}

public class D3D9ConstantDeclaration : ConstantDeclaration
{
    public RegisterSet RegisterSet { get; private set; }
    public short RegisterCount { get; }

    /// <summary>
    /// A uniform an fx_2_0 preshader reads. Its register is chosen by the parser to
    /// avoid the shader's own; the preshader numbered it in a separate table.
    /// </summary>
    public bool IsPreshaderInput { get; init; }

    public D3D9ConstantDeclaration(string name, RegisterSet registerSet, short registerIndex, short registerCount, ShaderTypeInfo typeInfo)
        : base(name, registerIndex, typeInfo)
    {
        RegisterSet = registerSet;
        RegisterCount = registerCount;
    }

    public bool ContainsIndex(int index)
    {
        return (index >= RegisterIndex) && (index < RegisterIndex + RegisterCount);
    }

    public override int RegistersPerElement =>
        System.Math.Max(RegisterCount / System.Math.Max(TypeInfo.NumElements, 1), 1);
}

public class D3D10ConstantDeclaration : ConstantDeclaration
{
    public int Offset { get; }
    public int VariableSize { get; }
    public int VariableOffset { get; }

    /// <summary>
    /// The declaring buffer. A cbuffer can be flattened into globals; a tbuffer
    /// binds to a t register and must keep its block and name.
    /// </summary>
    public string BufferName { get; init; }

    public bool IsTextureBuffer { get; init; }

    public D3D10ConstantDeclaration(string name, short registerIndex, int variableSize, int variableOffset, ShaderTypeInfo typeInfo, int offset)
        : base(name, registerIndex, typeInfo)
    {
        Offset = offset;
        VariableSize = variableSize;
        VariableOffset = variableOffset;
    }

    // Array elements are 16-byte aligned.
    public override int RegistersPerElement =>
        System.Math.Max((VariableSize / System.Math.Max(TypeInfo.NumElements, 1) + 15) / 16, 1);
}

// https://learn.microsoft.com/en-us/windows-hardware/drivers/display/dcl-instruction
public class RegisterDeclaration
{
    public RegisterDeclaration(RegisterKey registerKey, string semantic, int writeMask, ResultModifier resultModifier)
    {
        RegisterKey = registerKey;
        Semantic = semantic;
        WriteMask = writeMask;
        ResultModifier = resultModifier;
    }

    public RegisterDeclaration(RegisterKey registerKey, string semantic, int writeMask)
        : this(registerKey, semantic, writeMask, ResultModifier.None)
    {
    }

    // D3D_REGISTER_COMPONENT_TYPE from the signature: 1 uint32, 2 sint32, 3 float32.
    public int ComponentType { get; set; }

    public D3D10InterpolationMode InterpolationMode { get; init; }

    private string InterpolationModifier => InterpolationMode switch
    {
        D3D10InterpolationMode.Constant => "nointerpolation ",
        D3D10InterpolationMode.LinearCentroid => "centroid ",
        D3D10InterpolationMode.LinearNoPerspective => "noperspective ",
        D3D10InterpolationMode.LinearNoPerspectiveCentroid => "noperspective centroid ",
        D3D10InterpolationMode.LinearSample => "sample ",
        D3D10InterpolationMode.LinearNoPerspectiveSample => "noperspective sample ",
        _ => "",
    };

    public RegisterKey RegisterKey { get; }
    public string Semantic { get; }
    public ResultModifier ResultModifier { get; }

    /// <summary>
    /// A name the semantic does not give, e.g. edges[1] for a tessellation factor
    /// array spread across registers.
    /// </summary>
    public string NameOverride { get; set; }

    public string Name => NameOverride ?? Hlsl.HlslKeywords.Escape(Semantic.ToLower());

    /// <summary>
    /// Whether the register holds integers. Not asked of TypeName:
    /// `nointerpolation float4` contains "int".
    /// </summary>
    public bool IsInteger =>
        (RegisterKey is D3D10RegisterKey d3D10RegisterKey
            && D3D10Instruction.IsThreadRegister(d3D10RegisterKey.OperandType))
        || ComponentType is 1 or 2;

    public string TypeName
    {
        get
        {
            string centroid = ResultModifier.HasFlag(ResultModifier.Centroid) ? "centroid " : "";
            string type;
            if (RegisterKey is D3D10RegisterKey d3D10RegisterKey
                && D3D10Instruction.IsThreadRegister(d3D10RegisterKey.OperandType))
            {
                type = "uint";
            }
            else if (ComponentType == 1)
            {
                type = "uint";
            }
            else if (ComponentType == 2)
            {
                type = "int";
            }
            else
            {
                type = ResultModifier.HasFlag(ResultModifier.PartialPrecision) ? "half" : "float";
            }
            string length = MaskedLength > 1 ? MaskedLength.ToString() : "";
            return InterpolationModifier + centroid + type + length;
        }
    }

    /// <summary>
    /// Registers covered by a dcl_indexrange array, e.g. `float4 texcoord[4] :
    /// TEXCOORD`; otherwise one.
    /// </summary>
    public int ArrayLength { get; set; } = 1;

    public int WriteMask { get; set; }

    // PSIZE and FOG must be scalar to compile, whatever the declared mask.
    private bool IsScalarSemantic => Semantic == "PSIZE" || Semantic == "FOG";

    // For a packed register whose mask does not start at x, where the
    // highest-bit-plus-one rule would count the other declaration's components.
    public int? MaskedLengthOverride { get; set; }

    // A domain shader's patch constant, whose name comes from the signature.
    public RegisterSignature PatchConstantSignature { get; set; }

    // Highest written component plus one: .xy is 2, .yw is 4.
    public int MaskedLength
    {
        get
        {
            if (IsScalarSemantic)
            {
                return 1;
            }
            if (MaskedLengthOverride.HasValue)
            {
                return MaskedLengthOverride.Value;
            }
            for (int i = 3; i >= 0; i--)
            {
                if ((WriteMask & (1 << i)) != 0)
                {
                    return i + 1;
                }
            }
            return 0;
        }
    }

    public override string ToString()
    {
        return RegisterKey.ToString() + " " + Name;
    }
}
