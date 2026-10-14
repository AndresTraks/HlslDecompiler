using System.Globalization;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// An immediate double, d(). Not a ConstantNode, so float-only templates cannot
/// fold it and lose precision.
/// </summary>
public class DoubleConstantNode : HlslTreeNode
{
    public double Value { get; }

    public DoubleConstantNode(double value)
    {
        Value = value;
    }

    public override bool Equals(object obj)
    {
        return obj is DoubleConstantNode other && Value == other.Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
