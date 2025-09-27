using System;

namespace HlslDecompiler.DirectXShaderModel
{
    public class ConstantDeclaration
    {
        public string Name { get; private set; }
        public RegisterSet RegisterSet { get; private set; }
        public short RegisterIndex { get; private set; }
        public short RegisterCount { get; private set; }
        public ParameterClass ParameterClass { get; private set; }
        public ParameterType ParameterType { get; private set; }
        public int Rows { get; private set; }
        public int Columns { get; set; }

        public ConstantDeclaration(string name, RegisterSet registerSet, short registerIndex, short registerCount,
            ParameterClass parameterClass, ParameterType parameterType, int rows, int columns)
        {
            Name = name;
            RegisterSet = registerSet;
            RegisterIndex = registerIndex;
            RegisterCount = registerCount;
            ParameterClass = parameterClass;
            ParameterType = parameterType;
            Rows = rows;
            Columns = columns;
        }

        public bool ContainsIndex(int index)
        {
            return (index >= RegisterIndex) && (index < RegisterIndex + RegisterCount);
        }

        public int GetSamplerDimension()
        {
            return ParameterType switch
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

        public RegisterDeclaration(RegisterKey registerKey, string semantic, int maskedLength)
            : this(registerKey, semantic, maskedLength, ResultModifier.None)
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
