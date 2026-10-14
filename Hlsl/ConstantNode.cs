using System.Globalization;

namespace HlslDecompiler.Hlsl;

public class ConstantNode : HlslTreeNode
{
    public float Value { get; }

    /// <summary>
    /// Set when the 32 bits were an integer, which a float may not hold exactly.
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

    /// <summary>
    /// The negated constant, keeping IntegerValue exact where there is one.
    /// </summary>
    public ConstantNode Negated()
    {
        return IntegerValue != null
            ? new ConstantNode(-IntegerValue.Value)
            : new ConstantNode(-Value);
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
