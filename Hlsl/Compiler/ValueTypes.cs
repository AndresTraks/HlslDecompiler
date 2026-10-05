using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.FlowControl;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// What a value is, asked of the value rather than of the register it sits in.
///
/// fxc reuses a register freely, so the register says nothing: a float4 of texture
/// offsets was declared int4 for the loop counter that took its x over after the
/// loop. What a value is comes from three places, in this order - what reads it,
/// where the readers agree; the operation that made it, where they do not; and
/// nothing at all, which leaves the register's own type to answer.
///
/// These lived in StatementFinalizer, which is the pass that cuts the value graph
/// into statements and had no other business holding a type system. They are what
/// NodeCompiler and HlslAstWriter ask when they need to know whether to declare a
/// variable int or float, whether to write asfloat or a cast, and whether an
/// integer is a number or a float's bits.
/// </summary>
internal static class ValueTypes
{
    /// <summary>
    /// Whether a value is an integer, from the value rather than the register it
    /// was in: fxc reuses a register, and a float4 of texture offsets was declared
    /// int4 for the loop counter that took r0.x over after the loop. What reads
    /// the value decides where the readers agree; the operation that made it
    /// otherwise - an integer add makes an integer, a conversion what it converts
    /// to; and null - the register's own type - where neither says, as for a load,
    /// whose operands are integers whatever it loads, or an immediate nothing reads
    /// as either.
    /// </summary>
    internal static bool? IsIntegerValue(HlslTreeNode value)
    {
        // Bits are an integer and nothing else could be meant, so they are not put
        // to the readers: an integer add of two packed half floats read by a float
        // multiply was typed by the multiply and then computed in floats.
        if (IsBitsValue(value))
        {
            return true;
        }
        if (IsIntegerMadeReadAsFloat(value))
        {
            return true;
        }
        bool? consumedType = InstructionParser.GetConsumedType(value);
        bool? madeType = MadeType(value);
        if (consumedType != null || madeType != null)
        {
            return consumedType ?? madeType;
        }
        // A variable declared integer but read by nothing the graph can see - only by
        // the stores a value leaves a statement through, which are not output edges -
        // still holds an integer. The value an interlocked operation reads out of the
        // resource is one, and stored on into another: left asking its (absent) readers,
        // it printed as a float and the store into it converted back. Left null where
        // nothing says integer, which keeps the float the writers have always written.
        if (value is TempVariableNode variable && variable.IsInteger)
        {
            return true;
        }
        return null;
    }

    /// <summary>
    /// Whether an integer value is unsigned, decided the way its integer-ness is:
    /// by the readers where they agree, by what made it otherwise. ult and ilt both
    /// read as `a &lt; b`, and HLSL takes the signedness from the operands rather
    /// than from the operator, so a value only ever used unsigned is better declared
    /// uint than cast at each use. Null where nothing says either way, which leaves
    /// the int the writer has always declared.
    /// </summary>
    internal static bool? IsUnsignedValue(HlslTreeNode value)
    {
        return ConsumedAsUnsigned(value) ?? MadeUnsigned(value) ?? IndexesABuffer(value);
    }

    /// <summary>
    /// Whether the only thing that reads a value is a buffer subscript, which is an
    /// index and so unsigned. Weaker evidence than the rest and asked last: being
    /// used as an index says how it is used, not what it is, so a value something
    /// made signed stays signed - `(int)sv_position.x` is an ftoi and keeps a
    /// negative, whatever it goes on to index. Where nothing else says anything, an
    /// index computed from a thread id has no other reader at all, and left with no
    /// opinion it was declared int and then compared against a uint with a ult a few
    /// lines later.
    /// </summary>
    private static bool? IndexesABuffer(HlslTreeNode value)
    {
        return value.Outputs.Any(reader => reader is LoadStructuredNode load
            && ReferenceEquals(load.Address, value))
            ? true
            : null;
    }

    /// <summary>
    /// What the readers of a value agree its signedness is, looking through the
    /// operations that are the same instruction either way - a move, a phi, an add,
    /// a bitwise operator - or null where they disagree or none of them says.
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

    /// <summary>Whether this reader of the node reads it as unsigned, or null where
    /// it says nothing about signedness.</summary>
    private static bool? ReadsAsUnsigned(HlslTreeNode reader, HlslTreeNode node)
    {
        switch (reader)
        {
            // ieq and ine are the same comparison either way, so only the ordered
            // ones say anything.
            case ComparisonNode { IsInteger: true, Comparison: IfComparison.LT
                or IfComparison.LE or IfComparison.GT or IfComparison.GE } comparison:
                return comparison.IsUnsigned;
            // What is shifted, not by how much: ushr and ishr differ in what fills
            // the top bits of the value.
            case ShiftRightOperation shift when ReferenceEquals(shift.Value, node):
                return shift.IsUnsigned;
            // umin and umax read both operands as unsigned, imin and imax as signed.
            case MinimumOperation minimum:
                return minimum.IsUnsigned;
            case MaximumOperation maximum:
                return maximum.IsUnsigned;
            // udiv is the only integer divide there is - there is no signed opcode
            // for it - so an integer quotient or remainder is unsigned.
            case DivisionOperation { ConsumesInteger: true }:
            case ModuloOperation { ConsumesInteger: true }:
                return true;
            // ineg, which is meaningless on an unsigned value.
            case NegateOperation when IsIntegerValue(reader) == true:
                return false;
            // utof and itof, which answer the same float and read their source
            // differently. A value every reader turns into a float with a utof is
            // unsigned, and declaring it so is what keeps the one conversion one
            // instruction: an int among them splits the utof and costs an itof.
            case ConvertOperation { SourceUnsigned: bool sourceUnsigned } convert
                when ReferenceEquals(convert.Value, node):
                return sourceUnsigned;
            default:
                return null;
        }
    }

    /// <summary>
    /// Whether the reader is the same instruction signed or unsigned, so that what
    /// reads its result is what knows. imin and umin used to be here because the
    /// parse made one node of both; the node says which now, above.
    /// </summary>
    private static bool IsSignNeutral(HlslTreeNode reader, HlslTreeNode value)
    {
        // A select carries its branches and not its condition: which way the test
        // went says nothing about the number it chose between.
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

    /// <summary>What the operation that made a value makes it, signed or unsigned,
    /// or null where the operation does not say.</summary>
    private static bool? MadeUnsigned(HlslTreeNode value)
    {
        return value switch
        {
            // ftou and ftoi.
            ConvertOperation convert => convert.TargetType switch
            {
                "uint" => true,
                "int" => false,
                _ => null,
            },
            ShiftRightOperation shift => shift.IsUnsigned,
            MinimumOperation minimum => minimum.IsUnsigned,
            MaximumOperation maximum => maximum.IsUnsigned,
            // ubfe fills the top of the field with zeroes and ibfe with its sign.
            BitFieldExtractOperation extract => extract.IsUnsigned,
            DivisionOperation { ConsumesInteger: true } => true,
            ModuloOperation { ConsumesInteger: true } => true,
            NegateOperation => false,
            // What the buffer holds. Asked of the maker because an atomic reads its
            // value through a statement, which the node graph does not carry, so a
            // value only an atomic reads has no readers to be asked.
            LoadStructuredNode load => load.IsUnsignedElement,
            // msad4 sums differences of bytes into a uint4 - and takes one as the
            // accumulator, so a second msad over the first declared its variable int4
            // and handed it back through a conversion.
            Msad4Node => true,
            // The two words asuint takes a double apart into are uints.
            DoubleBitsNode => true,
            _ => null,
        };
    }

    /// <summary>
    /// What the operation that made a value makes, from that operation alone. Null
    /// where it does not say - a move, a phi, an immediate - which is where the
    /// readers are the only thing that knows.
    /// </summary>
    private static bool? MadeType(HlslTreeNode value)
    {
        return value switch
        {
            ConvertOperation convert => convert.TargetType is "int" or "uint",
            // Conversions too, and for the same reason: what they read and what they
            // make are opposite, so the operation's own ConsumesInteger below would
            // answer the wrong question. f16tof32 unpacking two halves into a
            // float2 had them written as an int2, which truncates them.
            FloatToHalfOperation => true,
            HalfToFloatOperation => false,
            // And asdouble, which takes uints and makes a double.
            BitsToDoubleOperation => false,
            // And the other way: a word of a double is a uint.
            DoubleBitsNode => true,
            ConstantNode constant => constant.IntegerValue != null,
            // A select of two constants makes what its arms are, and constants
            // have their kind already settled: a movc between two floats makes a
            // float, and an `and` folded over a condition between two integers -
            // the 1.0f's bits of an isinf test, say - makes an integer whatever
            // the bits are. Mixed arms say nothing.
            MoveConditionalOperation { Source1: ConstantNode first, Source2: ConstantNode second }
                => first.IntegerValue != null && second.IntegerValue != null ? true
                    : first.IntegerValue == null && second.IntegerValue == null ? false
                    : (bool?)null,
            // A comparison makes a mask, all ones or all zeroes, which is an
            // integer: a variable holding one beside the -1 the other branch moved
            // in was declared float, and the -1 printed as the NaN its bits are.
            ComparisonNode => true,
            // A byte address buffer hands back uints whatever was stored, and a
            // structured one hands back what its element type says, which is
            // asked of the reflection data elsewhere.
            LoadStructuredNode { IsRaw: true } => true,
            LoadStructuredNode load => load.IsIntegerElement,
            // And a typed load from the view's return type, for the same reason: the
            // instruction reads an address and says nothing about the texel.
            ResourceLoadNode load => load.IsIntegerTexel,
            // A consume is the same question again: the instruction reads a slot,
            // and the buffer's element type is what says what comes back.
            ConsumeNode consume => consume.IsIntegerElement,
            // What resinfo and bufinfo report is whatever the instruction asked for
            // them as: a size in texels or a count of elements is a uint, and the
            // same measurement taken as a float is one.
            ResourceInfoNode info => info.ReturnType == D3D10ResInfoReturnType.Uint,
            // A bitwise operator says nothing about what it was given - it carries
            // bits along - but what it makes is an integer whatever went in: HLSL
            // has no other type it could be. Left saying nothing, four masks came
            // out as a float4, and every integer use of them stopped compiling.
            BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or BitwiseNotOperation or ShiftLeftOperation or ShiftRightOperation
                or BitFieldExtractOperation or BitFieldInsertOperation => true,
            Operation operation => operation.ConsumesInteger,
            _ => null,
        };
    }

    /// <summary>
    /// Whether an integer value is a float's bits rather than a number. The two
    /// are the same type and want opposite things of a float reader: a loop
    /// counter is converted, a packed pair of half floats is reinterpreted, and
    /// nothing in the type says which.
    ///
    /// Bits enter the graph one way - an integer operation reading a float, which
    /// is where the decompiler writes an asint - and spread from there through the
    /// integer operations that carry them. A constant is not bits: `x &amp; 255` is
    /// bits because x is, not because 255 is.
    /// </summary>
    internal static bool IsBitsValue(HlslTreeNode value)
    {
        return IsBitsValue(value, HlslTreeNode.NewNodeSet());
    }

    /// <summary>
    /// Whether a value standing where a float is wanted is to be reinterpreted
    /// rather than converted: bits, or the dwords a byte address buffer hands back,
    /// which are uints whatever was stored and are read as a float with asfloat.
    /// Converted instead, a float's bits came back as the number they happen to
    /// make. Not bits in the variable's sense - the variable holding them is a
    /// uint, and rightly.
    /// </summary>
    internal static bool IsReinterpretedAsFloat(HlslTreeNode value)
    {
        // Read as a float and made as an integer without a conversion between:
        // those bits are a float's, and converting them hands out the number they
        // happen to make. A variable so read is declared for it already - see
        // IsBitsVariable; this is the same answer for a value inlined into the
        // float's expression, where an `and`-folded select reached a float output
        // through a move and its integer bits converted instead of selecting 1.0.
        // A constant is never the source of bits: its own typing is settled when
        // it is made, and the fold that wrote `x + x` as `2 * x` must not see its
        // 2 read as the denormal its bits are.
        // Not a typed load of an integer texel. That one is reinterpreted where it
        // is compiled, off the view's return type, so answering yes here as well
        // put an asfloat around an asfloat: `asfloat(asfloat(depth.Load(p).x))`.
        // The load began saying what it makes when the view was asked rather than
        // the instruction, which is what made this question reach it at all.
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
        // Asked of the operation rather than of the readers, so that this can be
        // what the readers are answered with.
        if (MadeType(value) != true || !visited.Add(value))
        {
            return false;
        }
        // Bits enter the graph through a bitwise operator or a shift reading a
        // float, and nowhere else - that read is the asint the writer puts there.
        // An integer add or a conversion reading one converts it, and the integer
        // that comes out is a number: `(int)(16 * x)` is an address, and
        // `3 * n - 7` is arithmetic on a uniform. A constant is never the source
        // of either, whatever type it was given.
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
    /// Whether the variable a value is assigned to holds bits: it is an integer
    /// variable, and either the value is bits already or it is a float being
    /// reinterpreted on the way in.
    /// </summary>
    internal static bool IsBitsVariable(HlslTreeNode value, bool isInteger)
    {
        return isInteger
            && (IsBitsValue(value) || IsFloatMade(value) || IsIntegerMadeReadAsFloat(value));
    }

    /// <summary>
    /// Integer arithmetic whose result a float operation reads without converting
    /// it. Those bits are a float's: a shader that wanted the number they make
    /// would have put an itof or a utof between, and this one did not. bit_field
    /// assembles a float from a mantissa and an exponent with an iadd and
    /// multiplies what comes out.
    ///
    /// The two questions IsIntegerValue asks disagree here - the maker says
    /// integer and the readers say float - and taking the readers

    /// <summary>
    /// Integer arithmetic whose result a float operation reads without converting
    /// it. Those bits are a float's: a shader that wanted the number they make
    /// would have put an itof or a utof between, and this one did not. bit_field
    /// assembles a float from a mantissa and an exponent with an iadd and
    /// multiplies what comes out.
    ///
    /// The two questions IsIntegerValue asks disagree here - the maker says
    /// integer and the readers say float - and taking the readers' answer turns
    /// the bits into whatever number they happen to be. What tells this apart from
    /// an extracted field, which is a number and is read as one, is that the field
    /// has a conversion in front of it, so its readers say integer too.
    /// </summary>
    private static bool IsIntegerMadeReadAsFloat(HlslTreeNode value)
    {
        return MadeType(value) == true && InstructionParser.GetConsumedType(value) == false;
    }
}
