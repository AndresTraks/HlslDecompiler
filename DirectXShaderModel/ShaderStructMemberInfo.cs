namespace HlslDecompiler.DirectXShaderModel;

public class ShaderStructMemberInfo
{
    public string Name { get; }
    public ShaderTypeInfo TypeInfo { get; }

    /// <summary>Maps a structured buffer load's byte offset to a member.</summary>
    public int ByteOffset { get; }

    public ShaderStructMemberInfo(string name, ShaderTypeInfo typeInfo, int byteOffset = 0)
    {
        Name = name;
        TypeInfo = typeInfo;
        ByteOffset = byteOffset;
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
