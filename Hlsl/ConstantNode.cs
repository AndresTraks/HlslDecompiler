using System.Globalization;

namespace HlslDecompiler.Hlsl;

public class ConstantNode : HlslTreeNode
{
    public float Value { get; }

    public ConstantNode(float value)
    {
        Value = value;
    }

    public override bool Equals(object obj)
    {
        return obj is ConstantNode && this == (ConstantNode)obj;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }
    public static bool operator ==(ConstantNode x, ConstantNode y)
    {
        if (x is null) return y is null;
        if (y is null) return false;
        return x.Value == y.Value;
    }
    public static bool operator !=(ConstantNode x, ConstantNode y)
    {
        return !(x == y);
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
