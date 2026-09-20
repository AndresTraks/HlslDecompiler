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

    /// <summary>
    /// The same constant with the opposite sign, through the integer constructor
    /// where it was an integer - which is what keeps IntegerValue, and with it the
    /// value. Negating Value alone turned the 0x9E3779B9 of a hash into
    /// -1640531584, that number's nearest float, where the bytecode meant
    /// -1640531527.
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
