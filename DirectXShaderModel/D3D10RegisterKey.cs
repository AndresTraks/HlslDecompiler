using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

public class D3D10RegisterKey : RegisterKey
{
    public D3D10RegisterKey(OperandType operandType, int registerNumber)
    {
        OperandType = operandType;
        Number = registerNumber;
    }

    /// <summary>
    /// An output of one stream of a multi-stream geometry shader. Each stream numbers
    /// its own o0 upwards, so the stream is part of the key; null with one stream.
    /// </summary>
    public D3D10RegisterKey(OperandType operandType, int registerNumber, int? stream, bool onStream)
        : this(operandType, registerNumber)
    {
        Stream = onStream ? stream : null;
    }

    public int? Stream { get; }

    public D3D10RegisterKey(OperandType operandType, int registerNumber, int constantBufferOffset)
        : this(operandType, registerNumber)
    {
        ConstantBufferOffset = constantBufferOffset;
    }

    public D3D10RegisterKey(float[] immediateSingle)
    {
        OperandType = OperandType.Immediate32;
        ImmediateSingle = immediateSingle;
    }

    public D3D10RegisterKey(double[] immediateDouble)
    {
        OperandType = OperandType.Immediate64;
        ImmediateDouble = immediateDouble;
    }

    private D3D10RegisterKey(int gsAttribute, int gsVertex)
        : this(OperandType.Input, gsAttribute)
    {
        GSVertex = gsVertex;
    }

    public static D3D10RegisterKey CreateGSInput(int attribute, int vertex)
    {
        return new D3D10RegisterKey(attribute, vertex);
    }

    /// <summary>
    /// A control point output read back by the patch constant phase (`vocp[2][0]`).
    /// The operand type keeps it apart from the input patch, which is also readable.
    /// </summary>
    public static D3D10RegisterKey CreateControlPointOutput(int attribute, int point)
    {
        return new D3D10RegisterKey(OperandType.OutputControlPoint, attribute) { GSVertex = point };
    }

    /// <summary>The same register of another vertex or control point.</summary>
    public D3D10RegisterKey WithVertex(int vertex)
    {
        return new D3D10RegisterKey(OperandType, Number) { GSVertex = vertex };
    }

    /// <summary>Another attribute of the same vertex or control point.</summary>
    public D3D10RegisterKey WithAttribute(int attribute)
    {
        return new D3D10RegisterKey(OperandType, attribute) { GSVertex = GSVertex };
    }

    public OperandType OperandType { get; }
    public int Number { get; }
    public int? ConstantBufferOffset { get; }
    public float[] ImmediateSingle { get; }
    public double[] ImmediateDouble { get; }
    public int? ImmediateInt { get; }
    public int? GSVertex { get; private init; }

    public bool IsTempRegister => OperandType == OperandType.Temp;
    // Depth, coverage and stencil ref are outputs too, though they name no register.
    public bool IsOutput =>
        OperandType == OperandType.Output ||
        OperandType == OperandType.OutputDepth ||
        OperandType == OperandType.OutputDepthGreaterEqual ||
        OperandType == OperandType.OutputDepthLessEqual ||
        OperandType == OperandType.OutputCoverageMask ||
        OperandType == OperandType.OutputStencilRef;
    public bool IsConstant =>
        OperandType == OperandType.ConstantBuffer ||
        OperandType == OperandType.Immediate32 ||
        OperandType == OperandType.Immediate64 ||
        OperandType == OperandType.ImmediateConstantBuffer;

    public D3D10RegisterKey GetGSBaseKey()
    {
        if (GSVertex.HasValue)
        {
            return new D3D10RegisterKey(OperandType, Number);
        }
        return this;
    }

    public bool TypeEquals(RegisterKey registerKey)
    {
        if (registerKey is not D3D10RegisterKey other)
        {
            return false;
        }
        return other.OperandType == OperandType;
    }

    public override bool Equals(object obj)
    {
        if (obj is not D3D10RegisterKey other)
        {
            return false;
        }
        if (other.ImmediateSingle == null)
        {
            if (ImmediateSingle != null)
            {
                return false;
            }
        }
        else
        {
            if (other.ImmediateSingle.Length != ImmediateSingle.Length)
            {
                return false;
            }
            for (int i = 0; i < ImmediateSingle.Length; i++)
            {
                if (other.ImmediateSingle[i] != ImmediateSingle[i])
                {
                    return false;
                }
            }
        }
        if (other.ImmediateDouble == null)
        {
            if (ImmediateDouble != null)
            {
                return false;
            }
        }
        else
        {
            if (ImmediateDouble == null)
            {
                return false;
            }
            if (other.ImmediateDouble.Length != ImmediateDouble.Length)
            {
                return false;
            }
            for (int i = 0; i < ImmediateDouble.Length; i++)
            {
                if (other.ImmediateDouble[i] != ImmediateDouble[i])
                {
                    return false;
                }
            }
        }
        return
            other.Number == Number &&
            other.OperandType == OperandType &&
            other.Stream == Stream &&
            other.ConstantBufferOffset == ConstantBufferOffset &&
            other.ImmediateInt == ImmediateInt &&
            other.GSVertex == GSVertex;
    }

    public override int GetHashCode()
    {
        int hashCode =
            Number.GetHashCode() ^
            OperandType.GetHashCode();
        if (ConstantBufferOffset != null)
        {
            hashCode ^= ConstantBufferOffset.GetHashCode();
        }
        if (ImmediateSingle != null)
        {
            for (int i = 0; i < ImmediateSingle.Length; i++)
            {
                hashCode ^= ImmediateSingle[i].GetHashCode();
            }
        }
        if (ImmediateDouble != null)
        {
            for (int i = 0; i < ImmediateDouble.Length; i++)
            {
                hashCode ^= ImmediateDouble[i].GetHashCode();
            }
        }
        if (ImmediateInt != null)
        {
            hashCode ^= ImmediateInt.GetHashCode();
        }
        if (GSVertex != null)
        {
            hashCode ^= GSVertex.GetHashCode();
        }
        if (Stream != null)
        {
            hashCode ^= Stream.GetHashCode();
        }
        return hashCode;
    }

    public override string ToString()
    {
        if (ImmediateSingle != null)
        {
            if (ImmediateSingle.Length == 1)
            {
                return ImmediateSingle[0].ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return $"[{string.Join(", ", ImmediateSingle.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)))}]";
        }
        if (ImmediateDouble != null)
        {
            return $"d({string.Join(", ", ImmediateDouble.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)))})";
        }
        if (ImmediateInt.HasValue)
        {
            return ImmediateInt.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (GSVertex != null)
        {
            return $"{OperandType}[{GSVertex}][{Number}]";
        }
        string constantBufferOffset = ConstantBufferOffset != null ? $"[{ConstantBufferOffset}]" : "";
        string stream = Stream != null ? $"@m{Stream}" : "";
        return $"{OperandType}{Number}{constantBufferOffset}{stream}";
    }
}
