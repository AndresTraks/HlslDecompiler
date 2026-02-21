namespace HlslDecompiler.DirectXShaderModel;

public class ShaderStructMemberInfo
{
    public string Name { get; }
    public ShaderTypeInfo TypeInfo { get; }

    public ShaderStructMemberInfo(string name, ShaderTypeInfo typeInfo)
    {
        Name = name;
        TypeInfo = typeInfo;
    }

    public override string ToString()
    {
        return Name + " " + TypeInfo;
    }

    public override bool Equals(object obj)
    {
        if (obj is not ShaderStructMemberInfo info)
        {
            return false;
        }
        return Name.Equals(info.Name) && TypeInfo.Equals(info.TypeInfo);
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode() ^ TypeInfo.GetHashCode();
    }
}
