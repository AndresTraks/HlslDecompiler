using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
{
    private int _numTextureCoordinates;

    private TextureLoadOutputNode(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex, RegisterInputNode texture)
    {
        AddInput(sampler);
        foreach (HlslTreeNode textureCoord in textureCoords)
        {
            AddInput(textureCoord);
        }
        if (texture != null)
        {
            AddInput(texture);
        }
        _numTextureCoordinates = textureCoords.Length;
        ComponentIndex = componentIndex;
    }

    public static TextureLoadOutputNode Create(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex, RegisterInputNode texture)
    {
        return new TextureLoadOutputNode(sampler, textureCoords, componentIndex, texture);
    }

    public static TextureLoadOutputNode Create(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
    {
        return new TextureLoadOutputNode(sampler, textureCoords, componentIndex, null);
    }

    public static TextureLoadOutputNode CreateBias(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
    {
        return new TextureLoadOutputNode(sampler, textureCoords, componentIndex, null)
        {
            Controls = TextureLoadControls.Bias
        };
    }

    public static TextureLoadOutputNode CreateLod(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
    {
        return new TextureLoadOutputNode(sampler, textureCoords, componentIndex, null)
        {
            Controls = TextureLoadControls.Lod
        };
    }

    public static TextureLoadOutputNode CreateProj(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex)
    {
        return new TextureLoadOutputNode(sampler, textureCoords, componentIndex, null)
        {
            Controls = TextureLoadControls.Project
        };
    }

    public static TextureLoadOutputNode CreateGrad(RegisterInputNode sampler, HlslTreeNode[] textureCoords, int componentIndex,
        HlslTreeNode[] derivativeX, HlslTreeNode[] derivativeY)
    {
        var node = new TextureLoadOutputNode(sampler, textureCoords, componentIndex, null)
        {
            Controls = TextureLoadControls.Grad
        };
        foreach (HlslTreeNode component in derivativeX)
        {
            node.AddInput(component);
        }
        foreach (HlslTreeNode component in derivativeY)
        {
            node.AddInput(component);
        }
        return node;
    }

    public RegisterInputNode Sampler => (RegisterInputNode)Inputs[0];
    public IEnumerable<HlslTreeNode> TextureCoordinateInputs => Inputs.Skip(1).Take(_numTextureCoordinates);
    public IEnumerable<HlslTreeNode> DerivativeX => Inputs
        .Skip(1 + _numTextureCoordinates)
        .Take(Controls.HasFlag(TextureLoadControls.Grad) ? _numTextureCoordinates : 0);
    public IEnumerable<HlslTreeNode> DerivativeY => Inputs
        .Skip(1 + 2 * _numTextureCoordinates)
        .Take(Controls.HasFlag(TextureLoadControls.Grad) ? _numTextureCoordinates : 0);
    public RegisterInputNode Texture => Inputs
        .Skip(1 + _numTextureCoordinates + (Controls.HasFlag(TextureLoadControls.Grad) ? 2 * _numTextureCoordinates : 0))
        .FirstOrDefault() as RegisterInputNode;
    public int ComponentIndex { get; }
    public TextureLoadControls Controls { get; private set; }

    public override string ToString()
    {
        return $"tex({Sampler}, {TextureCoordinateInputs
            .Select(i => i.ToString())
            .Aggregate((a, b) => a + ", " + b)})";
    }
}

[Flags]
public enum TextureLoadControls
{
    None = 0,
    Bias = 1,
    Lod = 2,
    Grad = 4,
    Project = 8
}
