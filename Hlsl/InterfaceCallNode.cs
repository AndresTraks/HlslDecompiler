using HlslDecompiler.DirectXShaderModel;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A call through an interface instance. Arguments are the registers the body
/// reads as parameters, as wide as each is declared.
/// </summary>
public class InterfaceCallNode : HlslTreeNode, IHasComponentIndex
{
    private readonly int[] _argumentWidths;

    public InterfaceCallNode(int interfaceNumber, int instanceIndex, int functionIndex,
        HlslTreeNode[] arguments, int[] argumentWidths, int componentIndex)
    {
        foreach (HlslTreeNode argument in arguments)
        {
            AddInput(argument);
        }
        _argumentWidths = argumentWidths;
        InterfaceNumber = interfaceNumber;
        InstanceIndex = instanceIndex;
        FunctionIndex = functionIndex;
        ComponentIndex = componentIndex;
    }

    public int InterfaceNumber { get; }
    public int InstanceIndex { get; }
    public int FunctionIndex { get; }
    public int ComponentIndex { get; }

    /// <summary>The component reads of all arguments, one after another.</summary>
    public System.Collections.Generic.IEnumerable<HlslTreeNode> Arguments => Inputs;

    /// <summary>The component reads of each argument.</summary>
    public System.Collections.Generic.IEnumerable<HlslTreeNode[]> ArgumentComponents =>
        _argumentWidths.Select((width, argument) =>
            Inputs.Skip(_argumentWidths.Take(argument).Sum()).Take(width).ToArray());

    public override string ToString()
    {
        return $"g{InterfaceNumber}[{InstanceIndex}].F{FunctionIndex}({string.Join(", ",
            ArgumentComponents.Select(a => a.First()))})";
    }
}
