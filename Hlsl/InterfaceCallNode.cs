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

    /// <param name="index">
    /// The instance array index read at run time (`fcall fp0[r0.w + k]`), which the
    /// instance index is then added to; null for an immediate instance.
    /// </param>
    public InterfaceCallNode(int interfaceNumber, int instanceIndex, int functionIndex,
        HlslTreeNode[] arguments, int[] argumentWidths, int componentIndex, HlslTreeNode index = null)
    {
        foreach (HlslTreeNode argument in arguments)
        {
            AddInput(argument);
        }
        // Last, so the arguments keep their places.
        if (index != null)
        {
            AddInput(index);
        }
        _argumentWidths = argumentWidths;
        InterfaceNumber = interfaceNumber;
        InstanceIndex = instanceIndex;
        FunctionIndex = functionIndex;
        ComponentIndex = componentIndex;
        HasIndex = index != null;
    }

    public int InterfaceNumber { get; }
    public int InstanceIndex { get; }
    public int FunctionIndex { get; }
    public int ComponentIndex { get; }

    public bool HasIndex { get; }

    /// <summary>The run-time instance index, or null.</summary>
    public HlslTreeNode Index => HasIndex ? Inputs[^1] : null;

    /// <summary>The component reads of all arguments, one after another.</summary>
    public System.Collections.Generic.IEnumerable<HlslTreeNode> Arguments =>
        HasIndex ? Inputs.Take(Inputs.Count - 1) : Inputs;

    /// <summary>The component reads of each argument.</summary>
    public System.Collections.Generic.IEnumerable<HlslTreeNode[]> ArgumentComponents =>
        _argumentWidths.Select((width, argument) =>
            Inputs.Skip(_argumentWidths.Take(argument).Sum()).Take(width).ToArray());

    public override string ToString()
    {
        string instance = HasIndex ? $"{Index} + {InstanceIndex}" : InstanceIndex.ToString();
        return $"g{InterfaceNumber}[{instance}].F{FunctionIndex}({string.Join(", ",
            ArgumentComponents.Select(a => a.First()))})";
    }
}
