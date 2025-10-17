using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public class ConstantDeclaration
    {
        public string Name { get; }
        public short RegisterIndex { get; }
        public short RegisterCount { get; }
        public ShaderTypeInfo TypeInfo { get; }

        public ConstantDeclaration(string name, short registerIndex, short registerCount, ShaderTypeInfo typeInfo)
        {
            Name = name;
            RegisterIndex = registerIndex;
            RegisterCount = registerCount;
            TypeInfo = typeInfo;
        }

        public bool ContainsIndex(int index)
        {
            return (index >= RegisterIndex) && (index < RegisterIndex + RegisterCount);
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

        public override string ToString()
        {
            return Name;
        }
    }

    public class D3D9ConstantDeclaration : ConstantDeclaration
    {
        public RegisterSet RegisterSet { get; private set; }

        public D3D9ConstantDeclaration(string name, RegisterSet registerSet, short registerIndex, short registerCount, ShaderTypeInfo typeInfo)
            : base(name, registerIndex, registerCount, typeInfo)
        {
            RegisterSet = registerSet;
        }
    }

    public class D3D10ConstantDeclaration : ConstantDeclaration
    {
        public int Offset { get; }

        public D3D10ConstantDeclaration(string name, short registerIndex, short registerCount, ShaderTypeInfo typeInfo, int offset)
            : base(name, registerIndex, registerCount, typeInfo)
        {
            Offset = offset;
        }
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

        public RegisterKey RegisterKey { get; }
        public string Semantic { get; }
        public ResultModifier ResultModifier { get; }

        public string Name => Semantic.ToLower();

        public string TypeName
        {
            get
            {
                string centroid = ResultModifier.HasFlag(ResultModifier.Centroid) ? "centroid " : "";
                string type = ResultModifier.HasFlag(ResultModifier.PartialPrecision) ? "half" : "float";
                string length = MaskedLength > 1 ? MaskedLength.ToString() : "";
                return centroid + type + length;
            }
        }

        public int WriteMask { get; set; }

        // Length of ".xy" = 2
        // Length of ".yw" = 4 (xyzw)
        public int MaskedLength
        {
            get
            {
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
}
