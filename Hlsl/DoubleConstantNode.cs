using System.Globalization;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A double from the bytecode itself - the d() an immediate operand carries -
/// where a constant buffer's double is read through its register. Not a
/// ConstantNode: the operation over two doubles is the same node as the
/// operation over two floats, and a fold that reads the value as a float would
/// quietly drop half of it. Being its own type is what keeps it out of the
/// float-only templates, and its membership in the set of doubles is what
/// types the expression built on top of it.
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
