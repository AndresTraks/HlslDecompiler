namespace HlslDecompiler
{
    // D3DXREGISTER_SET
    public enum RegisterSet
    {
        Bool,
        Int4,
        Float4,
        Sampler
    }

    // D3DXPARAMETER_CLASS
    public enum ParameterClass
    {
        Scalar,
        Vector,
        MatrixRows,
        MatrixColumns,
        Object,
        Struct
    }

    // D3DXPARAMETER_TYPE
    public enum ParameterType
    {
        Void,
        Bool,
        Int,
        Float,
        String,
        Texture,
        Texture1D,
        Texture2D,
        Texture3D,
        TextureCube,
        Sampler,
        Sampler1D,
        Sampler2D,
        Sampler3D,
        SamplerCube,
        PixelShader,
        VertexShader,
        PixelFragment,
        VertexFragment,
        Unsupported
    }

    // D3DDECLUSAGE
    public enum DeclUsage
    {
        Position,
        BlendWeight,
        BlendIndices,
        Normal,
        PSize,
        TexCoord,
        Tangent,
        Binormal,
        TessFactor,
        PositionT,
        Color,
        Fog,
        Depth,
        Sample
    }

    // D3DSAMPLER_TEXTURE_TYPE
    public enum SamplerTextureType
    {
        Unknown = 1,
        TwoD = 2,
        Cube = 4,
        Volume = 8
    }

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

    // https://msdn.microsoft.com/en-us/library/windows/hardware/ff549176(v=vs.85).aspx
    public class RegisterDeclaration
    {
        public Instruction DeclInstruction { get; private set; }

        public RegisterType RegisterType { get { return DeclInstruction.GetParamRegisterType(1); } }
        public int RegisterNumber { get { return DeclInstruction.GetParamRegisterNumber(1); } }

        public string Semantic
        {
            get { return DeclInstruction.GetDeclSemantic(); }
        }

        public string Name
        {
            get { return DeclInstruction.GetDeclSemantic().ToLower(); }
        }

        public string TypeName
        {
            get
            {
                string typeName = "float";
                int length = DeclInstruction.GetDestinationMaskedLength();
                if (length > 1)
                {
                    typeName += length.ToString();
                }
                return typeName;
            }
        }

        public RegisterDeclaration(Instruction declInstruction)
        {
            DeclInstruction = declInstruction;
        }

        public override string ToString()
        {
            return RegisterType + " " + Name;
        }
    }
}
