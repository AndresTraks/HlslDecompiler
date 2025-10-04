using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel
{
    public sealed class ConstantTable
    {
        public int MinorVersion { get; }
        public int MajorVersion { get; }
        public ShaderType ShaderType { get; }
        public ShaderFlags ShaderFlags { get; }
        public string CompilerInfo { get; }
        public string ShaderModel { get; }
        public IList<D3D9ConstantDeclaration> Declarations { get; }

        public ConstantTable()
        {
            Declarations = [];
        }

        public ConstantTable(int minorVersion, int majorVersion, ShaderType shaderType, ShaderFlags shaderFlags, string compilerInfo, string shaderModel, IList<D3D9ConstantDeclaration> declarations)
        {
            MinorVersion = minorVersion;
            MajorVersion = majorVersion;
            ShaderType = shaderType;
            ShaderFlags = shaderFlags;
            CompilerInfo = compilerInfo;
            ShaderModel = shaderModel;
            Declarations = declarations;
        }
    }
}
