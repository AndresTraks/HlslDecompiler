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

        public override string ToString()
        {
            return Name;
        }
    }

    // https://docs.microsoft.com/en-us/windows-hardware/drivers/display/dcl-instruction
    public class RegisterDeclaration
    {
        private readonly int _maskedLength;

        public RegisterDeclaration(RegisterKey registerKey, string semantic, int maskedLength)
        {
            RegisterKey = registerKey;
            Semantic = semantic;
            _maskedLength = maskedLength;
        }

        public RegisterKey RegisterKey { get; }
        public string Semantic { get; }
        public string Name => Semantic.ToLower();

        public string TypeName
        {
            get
            {
                switch (_maskedLength)
                {
                    case 1:
                        return "float";
                    case 2:
                        return "float2";
                    case 3:
                        return "float3";
                    case 4:
                        return "float4";
                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public override string ToString()
        {
            return RegisterKey.ToString() + " " + Name;
        }
    }
}
