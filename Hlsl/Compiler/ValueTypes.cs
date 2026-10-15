using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// The evidence about a value's type. Made decides how it computes and whether
/// reading it as the other type reinterprets it; Consumed decides its declaration.
/// </summary>
internal sealed record TypeFacts(
    bool? Reads, bool? Made, bool? Consumed, bool Bits,
    bool? MadeUnsigned, bool? ConsumedUnsigned, bool? IndexesABuffer);

/// <summary>
/// What a value is, asked of the value rather than of the register it sits in,
/// since fxc reuses registers freely. Answered by the readers where they agree,
/// else by the operation that made it, else null (the register's own type).
/// </summary>
internal static class ValueTypes
{
    /// <summary>
    /// Records the facts for every reachable value. Must run before the finalizer
    /// and templates take readers away, since the answers depend on them.
    /// </summary>
    internal static void Record(IEnumerable<HlslTreeNode> roots)
    {
        var values = new List<HlslTreeNode>();
        var seen = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>(roots);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (node == null || !seen.Add(node))
            {
                continue;
            }
            values.Add(node);
            foreach (HlslTreeNode input in node.Inputs)
            {
                pending.Push(input);
            }
        }
        // All computed before any is stored: answers for one value ask about others.
        var facts = values.Select(value => new TypeFacts(
            value.ConsumesInteger, MadeType(value), InstructionParser.GetConsumedType(value),
            IsBitsValue(value, HlslTreeNode.NewNodeSet()),
            MadeUnsigned(value), ConsumedAsUnsigned(value), IndexesABuffer(value))).ToList();
        for (int i = 0; i < values.Count; i++)
        {
            values[i].Types = facts[i];
        }
    }

    /// <summary>
    /// Copies the replaced node's facts onto the nodes a template built for it,
    /// skipping nodes whose type is self-evident (comparisons, conversions,
    /// constants, variables).
    /// </summary>
    internal static void Inherit(HlslTreeNode replaced, HlslTreeNode replacement)
    {
        if (replaced.Types is not TypeFacts facts)
        {
            return;
        }
        var pending = new Stack<HlslTreeNode>();
        pending.Push(replacement);
        while (pending.Count != 0)
        {
            HlslTreeNode built = pending.Pop();
            if (built.Types != null
                || built is ComparisonNode or ConvertOperation or ConstantNode
                    or TempVariableNode or TempAssignmentNode or RegisterInputNode)
            {
                continue;
            }
            built.Types = facts;
            foreach (HlslTreeNode input in built.Inputs)
            {
                pending.Push(input);
            }
        }
    }

    private static bool? Made(HlslTreeNode value) =>
        value.Types is TypeFacts facts ? facts.Made : MadeType(value);

    private static bool? Consumed(HlslTreeNode value) =>
        value.Types is TypeFacts facts ? facts.Consumed : InstructionParser.GetConsumedType(value);

    /// <summary>
    /// Whether a value is an integer: by its readers where they agree, else by its
    /// maker, else null (the register's own type).
    /// </summary>
    internal static bool? IsIntegerValue(HlslTreeNode value)
    {
        // Bits are always an integer; a float reader must not retype them.
        if (IsBitsValue(value))
        {
            return true;
        }
        if (IsIntegerMadeReadAsFloat(value))
        {
            return true;
        }
        bool? consumedType = Consumed(value);
        bool? madeType = Made(value);
        if (consumedType != null || madeType != null)
        {
            return consumedType ?? madeType;
        }
        // A variable read only by statement stores (not output edges), e.g. an
        // interlocked result, still holds an integer if declared one.
        if (value is TempVariableNode variable && variable.IsInteger)
        {
            return true;
        }
        return null;
    }

    /// <summary>
    /// Whether an integer value is unsigned: by readers, else maker, else null (int).
    /// HLSL takes signedness from operands, not the operator (ult and ilt are both
    /// `a &lt; b`), so a value used only unsigned is declared uint.
    /// </summary>
    internal static bool? IsUnsignedValue(HlslTreeNode value)
    {
        if (value.Types is TypeFacts facts)
        {
            return facts.ConsumedUnsigned ?? facts.MadeUnsigned ?? facts.IndexesABuffer;
        }
        return ConsumedAsUnsigned(value) ?? MadeUnsigned(value) ?? IndexesABuffer(value);
    }

    /// <summary>
    /// Whether a value is used as a buffer address, hence unsigned. Weakest evidence,
    /// asked last: a value made signed (`(int)sv_position.x`) stays signed.
    /// </summary>
    private static bool? IndexesABuffer(HlslTreeNode value)
    {
        return value.Outputs.Any(reader => reader is LoadStructuredNode load
            && ReferenceEquals(load.Address, value))
            ? true
            : null;
    }

    /// <summary>
    /// The signedness the readers agree on, looking through sign-neutral operations;
    /// null where they disagree or none says.
    /// </summary>
    private static bool? ConsumedAsUnsigned(HlslTreeNode value)
    {
        bool? unsigned = null;
        var visited = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>();
        pending.Push(value);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            foreach (HlslTreeNode reader in node.Outputs)
            {
                if (!visited.Add(reader))
                {
                    continue;
                }
                bool? says = ReadsAsUnsigned(reader, node);
                if (says == null)
                {
                    if (IsSignNeutral(reader, node))
                    {
                        pending.Push(reader);
                    }
                    continue;
                }
                if (unsigned != null && unsigned != says)
                {
                    return null;
                }
                unsigned = says;
            }
        }
        return unsigned;
    }

    private static bool? ReadsAsUnsigned(HlslTreeNode reader, HlslTreeNode node)
    {
        switch (reader)
        {
            // ieq and ine are sign-neutral; only ordered comparisons say anything.
            case ComparisonNode { IsInteger: true, Comparison: IfComparison.LT
                or IfComparison.LE or IfComparison.GT or IfComparison.GE } comparison:
                return comparison.IsUnsigned;
            // Only the shifted value, not the shift amount.
            case ShiftRightOperation shift when ReferenceEquals(shift.Value, node):
                return shift.IsUnsigned;
            case MinimumOperation minimum:
                return minimum.IsUnsigned;
            case MaximumOperation maximum:
                return maximum.IsUnsigned;
            // udiv is the only integer divide; there is no signed opcode.
            case DivisionOperation or ModuloOperation when reader.ReadsIntegers == true:
                return true;
            case NegateOperation when IsIntegerValue(reader) == true:
                return false;
            // asdouble has only uint overloads.
            case BitsToDoubleOperation:
                return true;
            // utof vs itof: declaring the source uint keeps a utof one instruction.
            case ConvertOperation { SourceUnsigned: bool sourceUnsigned } convert
                when ReferenceEquals(convert.Value, node):
                return sourceUnsigned;
            default:
                return null;
        }
    }

    /// <summary>
    /// Whether the reader is the same instruction signed or unsigned, so its own
    /// readers decide.
    /// </summary>
    private static bool IsSignNeutral(HlslTreeNode reader, HlslTreeNode value)
    {
        // A select carries its branches, not its condition.
        if (reader is MoveConditionalOperation select)
        {
            return !ReferenceEquals(select.Inputs[0], value);
        }
        return reader is MoveOperation or PhiNode
            or AddOperation or SubtractOperation or MultiplyOperation
            or ShiftLeftOperation
            or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
            or BitwiseNotOperation;
    }

    private static bool? MadeUnsigned(HlslTreeNode value)
    {
        return value switch
        {
            ConvertOperation convert => convert.TargetType switch
            {
                "uint" => true,
                "int" => false,
                _ => null,
            },
            ShiftRightOperation shift => shift.IsUnsigned,
            MinimumOperation minimum => minimum.IsUnsigned,
            MaximumOperation maximum => maximum.IsUnsigned,
            BitFieldExtractOperation extract => extract.IsUnsigned,
            DivisionOperation or ModuloOperation when value.ReadsIntegers == true => true,
            NegateOperation => false,
            // Asked of the maker: an atomic reads through a statement, not an edge.
            LoadStructuredNode load => load.IsUnsignedElement,
            Msad4Node => true,
            DoubleBitsNode => true,
            _ => null,
        };
    }

    /// <summary>
    /// Whether an operation computes in integers. Asked of the operation, not its
    /// readers: a float multiply read only by integer ops is still a float multiply.
    /// </summary>
    internal static bool? ComputesInIntegers(HlslTreeNode operation)
    {
        return Made(operation);
    }

    /// <summary>Whether the value's original readers agree it is an integer.</summary>
    internal static bool? ReadAsInteger(HlslTreeNode value)
    {
        return Consumed(value);
    }

    /// <summary>
    /// Whether the making operation alone makes an integer; null for a move, phi or
    /// immediate, where only the readers know.
    /// </summary>
    private static bool? MadeType(HlslTreeNode value)
    {
        return value switch
        {
            ConvertOperation convert => convert.TargetType is "int" or "uint",
            // These read and make opposite types, so ReadsIntegers would be wrong.
            FloatToHalfOperation => true,
            HalfToFloatOperation => false,
            BitsToDoubleOperation => false,
            DoubleBitsNode => true,
            ConstantNode constant => constant.IntegerValue != null,
            // A select of two constants makes what its arms are; mixed arms say nothing.
            MoveConditionalOperation { Source1: ConstantNode first, Source2: ConstantNode second }
                => first.IntegerValue != null && second.IntegerValue != null ? true
                    : first.IntegerValue == null && second.IntegerValue == null ? false
                    : (bool?)null,
            // A comparison makes a mask (all ones or zeroes), an integer.
            ComparisonNode => true,
            // Raw buffers return uints; structured, typed and consumed loads take
            // the type from the resource, not the instruction.
            LoadStructuredNode { IsRaw: true } => true,
            LoadStructuredNode load => load.IsIntegerElement,
            ResourceLoadNode load => load.IsIntegerTexel,
            ConsumeNode consume => consume.IsIntegerElement,
            ResourceInfoNode info => info.ReturnType == D3D10ResInfoReturnType.Uint,
            // Bitwise ops make an integer whatever they read: HLSL has no other type.
            BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or BitwiseNotOperation or ShiftLeftOperation or ShiftRightOperation
                or BitFieldExtractOperation or BitFieldInsertOperation => true,
            Operation operation => operation.ReadsIntegers,
            _ => null,
        };
    }

    /// <summary>
    /// Whether an integer value is a float's bits (reinterpreted by a float reader)
    /// rather than a number (converted). Bits enter where a bitwise op reads a float
    /// and spread through integer ops; constants are never bits.
    /// </summary>
    internal static bool IsBitsValue(HlslTreeNode value)
    {
        if (value.Types is TypeFacts facts)
        {
            return facts.Bits;
        }
        return IsBitsValue(value, HlslTreeNode.NewNodeSet());
    }

    /// <summary>
    /// Whether a value where a float is wanted is reinterpreted (asfloat) rather
    /// than converted: bits, raw buffer dwords, or integer results read as float.
    /// </summary>
    internal static bool IsReinterpretedAsFloat(HlslTreeNode value)
    {
        // An integer-texel load is reinterpreted where it is compiled; answering
        // yes here too would write asfloat(asfloat(...)). Constants are excluded:
        // a folded `2 * x` must not read its 2 as a denormal.
        if (value is ResourceLoadNode { IsIntegerTexel: true })
        {
            return false;
        }
        return value is LoadStructuredNode { IsRaw: true } || IsBitsValue(value)
            || (value is not ConstantNode && IsIntegerMadeReadAsFloat(value));
    }

    private static bool IsBitsValue(HlslTreeNode value, HashSet<HlslTreeNode> visited)
    {
        if (value is TempVariableNode temp)
        {
            return temp.IsBits;
        }
        // Asked of the maker, not the readers, which this answer feeds.
        if (MadeType(value) != true || !visited.Add(value))
        {
            return false;
        }
        // Only a bitwise op or shift reading a float makes bits (the writer's asint);
        // arithmetic or a conversion reading one makes a number.
        bool reinterprets = value is BitwiseAndOperation or BitwiseOrOperation
            or BitwiseXorOperation or BitwiseNotOperation
            or ShiftLeftOperation or ShiftRightOperation
            or BitFieldExtractOperation or BitFieldInsertOperation;
        foreach (HlslTreeNode input in value.Inputs)
        {
            if (input is ConstantNode)
            {
                continue;
            }
            if ((reinterprets && IsFloatMade(input)) || IsBitsValue(input, visited))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsFloatMade(HlslTreeNode value)
    {
        return value is TempVariableNode temp ? !temp.IsInteger : MadeType(value) == false;
    }

    /// <summary>
    /// Whether an integer variable assigned this value holds a float's bits.
    /// </summary>
    internal static bool IsBitsVariable(HlslTreeNode value, bool isInteger)
    {
        return isInteger
            && (IsBitsValue(value) || IsFloatMade(value) || IsIntegerMadeReadAsFloat(value));
    }

    /// <summary>
    /// Integer arithmetic read by a float operation with no itof/utof between: the
    /// result is a float's bits (e.g. a float assembled from mantissa and exponent).
    /// </summary>
    private static bool IsIntegerMadeReadAsFloat(HlslTreeNode value)
    {
        return Made(value) == true && Consumed(value) == false;
    }
}
