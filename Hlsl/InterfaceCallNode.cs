using HlslDecompiler.DirectXShaderModel;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// A call through an interface: the method of whatever class is bound to the
/// instance when the shader runs. The arguments are what the call site had in the
/// registers the body reads as parameters - whole registers, four components each.
/// </summary>
public class InterfaceCallNode : HlslTreeNode, IHasComponentIndex
{
    private readonly int _argumentCount;

    public InterfaceCallNode(int interfaceNumber, int instanceIndex, int functionIndex,
        HlslTreeNode[] arguments, int componentIndex)
    {
        foreach (HlslTreeNode argument in arguments)
        {
            AddInput(argument);
        }
        _argumentCount = arguments.Length / 4;
        InterfaceNumber = interfaceNumber;
        InstanceIndex = instanceIndex;
        FunctionIndex = functionIndex;
        ComponentIndex = componentIndex;
    }

    public int InterfaceNumber { get; }
    public int InstanceIndex { get; }
    public int FunctionIndex { get; }
    public int ComponentIndex { get; }

    /// <summary>The arguments, four component reads per argument.</summary>
    public System.Collections.Generic.IEnumerable<HlslTreeNode> Arguments => Inputs;

    public int ArgumentCount => _argumentCount;

    public override string ToString()
    {
        return $"g{InterfaceNumber}[{InstanceIndex}].F{FunctionIndex}({string.Join(", ",
            Arguments.Chunk(4).Select(a => a.First()))})";
    }
}
