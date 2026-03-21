namespace HlslDecompiler.DirectXShaderModel;

public enum ShaderType
{
    Vertex = 0xFFFE,
    Pixel = 0xFFFF,
    Geometry = 0x4753,
    Hull = 0x4853,
    Domain = 0x4453,
    Compute = 0x4353
}
