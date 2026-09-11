using System.Globalization;

namespace HlslDecompiler.Hlsl;

public class ConstantNode : HlslTreeNode
{
    public float Value { get; }

    /// <summary>
    /// Set when the 32 bits were an integer. A float cannot hold every one of them -
    /// 1013904223 comes back as 1013904192 - and a shift amount written as a float
    /// does not compile.
    /// </summary>
    public int? IntegerValue { get; }

    public ConstantNode(float value)
    {
        Value = value;
    }

    public ConstantNode(int value)
    {
        Value = value;
        IntegerValue = value;
    }

    public override bool Equals(object obj)
    {
        return obj is ConstantNode other && this == other;
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
        return (IntegerValue?.ToString(CultureInfo.InvariantCulture))
            ?? Value.ToString(CultureInfo.InvariantCulture);
    }
}
