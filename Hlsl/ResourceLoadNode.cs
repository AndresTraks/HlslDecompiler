using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

// A typed resource load: Texture2D.Load(int3(x, y, mip)). Unlike a sample it takes
// no sampler - it reads the texel directly - so it does not fit
// TextureLoadOutputNode, whose operand layout is built around having one.
public class ResourceLoadNode : HlslTreeNode, IHasComponentIndex
{
    private readonly int _addressLength;

    public ResourceLoadNode(RegisterInputNode resource, HlslTreeNode[] address, int componentIndex)
    {
        AddInput(resource);
        foreach (HlslTreeNode component in address)
        {
            AddInput(component);
        }
        _addressLength = address.Length;
        ComponentIndex = componentIndex;
    }

    public RegisterInputNode Resource => (RegisterInputNode)Inputs[0];
    public IEnumerable<HlslTreeNode> Address => Inputs.Skip(1).Take(_addressLength);
    public int ComponentIndex { get; }

    public override string ToString()
    {
        return $"load({Resource}, {string.Join(", ", Address.Select(a => a.ToString()))})";
    }
}
