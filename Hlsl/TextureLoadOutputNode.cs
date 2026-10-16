using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class TextureLoadOutputNode : HlslTreeNode, IHasComponentIndex
{
    private int _numTextureCoordinates;
    private int _numOffsets;
    private bool _hasScalarArgument;

    // sample_aoffimmi's immediate texel offsets.
    public int[] SampleOffsets { get; set; }

    // A gather from a Texture2D<uint> or <int> returns its integers.
    public bool IsIntegerTexel { get; set; }

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

    // Input order, as the accessors expect: sampler, coordinates, gradients,
    // offsets, texture, then any scalar argument.
    public static TextureLoadOutputNode CreateSample(
        RegisterInputNode sampler,
        HlslTreeNode[] textureCoords,
        int componentIndex,
        RegisterInputNode texture,
        TextureLoadControls controls,
        HlslTreeNode[] derivativeX,
        HlslTreeNode[] derivativeY,
        HlslTreeNode scalarArgument,
        HlslTreeNode[] offsets = null)
    {
        var node = new TextureLoadOutputNode(sampler, textureCoords, componentIndex, null)
        {
            Controls = controls
        };
        if (derivativeX != null)
        {
            foreach (HlslTreeNode component in derivativeX)
            {
                node.AddInput(component);
            }
            foreach (HlslTreeNode component in derivativeY)
            {
                node.AddInput(component);
            }
        }
        if (offsets != null)
        {
            node._numOffsets = offsets.Length;
            foreach (HlslTreeNode component in offsets)
            {
                node.AddInput(component);
            }
        }
        node.AddInput(texture);
        if (scalarArgument != null)
        {
            node.AddInput(scalarArgument);
            node._hasScalarArgument = true;
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
    // gather4_po's register offset.
    public IEnumerable<HlslTreeNode> Offsets => Inputs
        .Skip(1 + _numTextureCoordinates
            + (Controls.HasFlag(TextureLoadControls.Grad) ? 2 * _numTextureCoordinates : 0))
        .Take(_numOffsets);
    public RegisterInputNode Texture => Inputs
        .Skip(1 + _numTextureCoordinates
            + (Controls.HasFlag(TextureLoadControls.Grad) ? 2 * _numTextureCoordinates : 0)
            + _numOffsets)
        .FirstOrDefault() as RegisterInputNode;
    // Level, bias or comparison value.
    public HlslTreeNode ScalarArgument => _hasScalarArgument ? Inputs[Inputs.Count - 1] : null;
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
    Project = 8,
    Compare = 16,
    LevelZero = 32,
    Gather = 64,
    // The lod instruction.
    CalculateLod = 128,
    // lod's unclamped level: resource swizzle `t0.y` rather than `t0.x`.
    Unclamped = 256,
    /// <summary>gather4_po: the texel offset comes from a register.</summary>
    ProgrammableOffset = 512,
}
