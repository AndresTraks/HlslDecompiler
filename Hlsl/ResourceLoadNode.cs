using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

// A typed resource load, Texture2D.Load(int3(x, y, mip)); no sampler, unlike
// TextureLoadOutputNode.
public class ResourceLoadNode : HlslTreeNode, IHasComponentIndex
{
    private readonly int _addressLength;

    public ResourceLoadNode(RegisterInputNode resource, HlslTreeNode[] address, int componentIndex,
        HlslTreeNode sampleIndex = null)
    {
        AddInput(resource);
        foreach (HlslTreeNode component in address)
        {
            AddInput(component);
        }
        _addressLength = address.Length;
        ComponentIndex = componentIndex;
        if (sampleIndex != null)
        {
            AddInput(sampleIndex);
            HasSampleIndex = true;
        }
    }

    public RegisterInputNode Resource => (RegisterInputNode)Inputs[0];
    public IEnumerable<HlslTreeNode> Address => Inputs.Skip(1).Take(_addressLength);

    // Texture2DMS.Load(int2, sampleIndex).
    public bool HasSampleIndex { get; }
    public HlslTreeNode SampleIndex => HasSampleIndex ? Inputs[1 + _addressLength] : null;
    public int ComponentIndex { get; }

    // The texel offsets of ld_aoffimmi, or null when the load has none.
    public int[] SampleOffsets { get; set; }

    // From the view's declared return type, since the instruction does not say;
    // null where there is no declaration.
    public bool? IsIntegerTexel { get; init; }

    public override string ToString()
    {
        return $"load({Resource}, {string.Join(", ", Address.Select(a => a.ToString()))})";
    }
}
