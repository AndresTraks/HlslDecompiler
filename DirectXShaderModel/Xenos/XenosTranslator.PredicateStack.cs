using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel.Xenos;

// The predicate stack as structure. XNA's compiler runs an if's body under p0,
// with a counter per nesting: setp_*_push makes it 0 where it was 0 and the
// condition holds, else one more; setp_pop one less; setp_inv swaps 0 and 1;
// setp_rstr sets p0 where it is 0; setp_clr puts it past any pop, a break. The
// counter is so the conditions pushed and still on it: 0 where they all hold,
// else the depth below the first that fails. Where every counter can be
// followed, the translation keeps those conditions instead, and runs predicated
// instructions in nested ifs on them: an else where the top one is inverted, a
// break where a loop's counter is cleared. A shader whose counters cannot be
// followed so is translated with the counter arithmetic.
public sealed partial class XenosTranslator
{
    private sealed class UnstructuredPredicateException(string message) : Exception(message);

    // A condition snapshot: 1 or 0 in a lane of a temp of its own.
    private sealed class Condition(int register, int lane)
    {
        public int Register { get; } = register;
        public int Lane { get; } = lane;
    }

    // A condition or its opposite. Without a condition, false.
    private readonly record struct Literal(Condition Condition, bool Negated);

    // The conditions p0 is the conjunction of: none where it is set, Never where
    // it is set nowhere, null where it cannot be told.
    private static readonly Literal[] Never = [new Literal(null, false)];

    private sealed class OpenCondition
    {
        public Literal Literal;
        public bool HasElse;
    }

    // What the loop's body starts with the first time round, and which of it
    // the body reads before setting: that must be what each time round ends with.
    private sealed class LoopState
    {
        public Dictionary<(int Register, int Lane), Literal[]> Counters;
        public HashSet<(int Register, int Lane)> CountersRead = [];
        public Literal[] P0;
        public bool P0Read;
        // Conditions a break reassigned: false now where the pixel broke out.
        public HashSet<Condition> Reassigned = [];
    }

    private readonly record struct PredicateState(
        Dictionary<(int Register, int Lane), Literal[]> Counters, Literal[] P0);

    private readonly bool _structuredPredicate;
    // The counters followed, by lane: the conditions on each.
    private readonly Dictionary<(int Register, int Lane), Literal[]> _counters = [];
    private Literal[] _p0;
    // The ifs open on conditions, outermost first.
    private readonly List<OpenCondition> _openConditions = [];
    private int _conditionCount;
    private int _lowestConditionRegister = 32;
    // p0 multiplied out into _predicate.x, while that holds.
    private Literal[] _materializedP0;
    private Condition _materializedCondition;
    private bool? _instructionCondition;
    // The code's length after a break, to tell an endloop's break repeats it.
    private int _breakEnd = -1;
    // The p0 a break just made was made unless, if it was.
    private Literal[] _brokeUnless;
    private readonly Stack<LoopState> _loops = new();
    // Since a break in the loop: the counters of the pixels that broke out,
    // where p0 stays 0 until the loop ends.
    private bool _broken;
    // The condition the instruction checked runs on, and the counters it reads as 0.
    private bool? _readCondition;
    private readonly List<(int Register, int Lane)> _zeroReads = [];
    private readonly HashSet<(int Register, int Lane)> _brokenCounters = [];

    private static bool IsNever(Literal[] p0) => p0.Length == 1 && p0[0].Condition == null;

    private Condition NewCondition()
    {
        int register = 31 - _conditionCount / 4;
        if (register <= _savedDirections || _cubesInFlight.Any(cube => cube.Register >= register))
        {
            throw new UnstructuredPredicateException("No temp left for a condition");
        }
        _lowestConditionRegister = register;
        return new Condition(register, _conditionCount++ % 4);
    }

    // The condition temps start at 0 like the shader's own: one computed in an
    // if is read only where that if's condition holds, but the HLSL must define it.
    private void DefineConditions(int body)
    {
        List<uint> code = _code.Skip(body).ToList();
        _code.RemoveRange(body, code.Count);
        for (int register = 31; register >= _lowestConditionRegister; register--)
        {
            Emit(Opcode.Mov, Destination(RegisterType.Temp, register, 0b1111), Zero());
        }
        _code.AddRange(code);
    }

    private static uint ConditionSource(Condition condition, SourceModifier modifier = SourceModifier.None) =>
        Source(RegisterType.Temp, condition.Register, Replicate(condition.Lane), modifier);

    private void SetP0(Literal[] p0)
    {
        _p0 = p0;
        ForgetBlockPredicates();
    }

    private Literal[] KnownP0()
    {
        Literal[] p0 = _p0 ?? throw new UnstructuredPredicateException("p0 read where it cannot be told");
        // The same array is the same p0, or one equal to it, which is as strict.
        foreach (LoopState loop in _loops)
        {
            loop.P0Read |= ReferenceEquals(p0, loop.P0);
        }
        return p0;
    }

    // The conditions an instruction predicated so runs on, null if it runs nowhere.
    private Literal[] Required(bool? condition)
    {
        if (condition == null
            || _blocks.Count != 0 && _blocks.Peek().Predicate == condition && !_blocks.Peek().HasElse)
        {
            return [];
        }
        Literal[] p0 = KnownP0();
        if (condition.Value)
        {
            return IsNever(p0) ? null : p0;
        }
        if (IsNever(p0))
        {
            return [];
        }
        return p0.Length switch
        {
            0 => null,
            1 => [p0[0] with { Negated = !p0[0].Negated }],
            _ => [Materialize(p0, negated: true)],
        };
    }

    // p0 as one condition: the product of its conditions, in _predicate.x.
    private Literal Materialize(Literal[] p0, bool negated)
    {
        if (!ReferenceEquals(_materializedP0, p0))
        {
            // Computed for every pixel, outside the ifs.
            CloseConditions();
            uint destination = Destination(RegisterType.Temp, _predicate, 0b0001);
            uint product = Source(RegisterType.Temp, _predicate, Replicate(0));
            for (int i = 0; i < p0.Length; i++)
            {
                Condition condition = p0[i].Condition;
                if (i == 0)
                {
                    if (p0[i].Negated)
                    {
                        Emit(Opcode.Add, destination, ConditionSource(condition, SourceModifier.Negate), One());
                    }
                    else
                    {
                        Emit(Opcode.Mov, destination, ConditionSource(condition));
                    }
                }
                else if (p0[i].Negated)
                {
                    // p * (1 - c) as p - p * c.
                    Emit(Opcode.Mad, destination, product, ConditionSource(condition, SourceModifier.Negate), product);
                }
                else
                {
                    Emit(Opcode.Mul, destination, product, ConditionSource(condition));
                }
            }
            _materializedP0 = p0;
            _materializedCondition = new Condition(_predicate, 0);
        }
        return new Literal(_materializedCondition, negated);
    }

    // Opens ifs on the conditions, keeping those open that they start with, and
    // turning the innermost that differs into its else where it is the opposite.
    private void OpenConditions(Literal[] required)
    {
        int shared = 0;
        while (shared < _openConditions.Count && shared < required.Length
            && _openConditions[shared].Literal == required[shared])
        {
            shared++;
        }
        if (shared < _openConditions.Count && shared < required.Length
            && _openConditions[shared].Literal.Condition == required[shared].Condition
            && !_openConditions[shared].HasElse)
        {
            CloseConditions(shared + 1);
            Emit(Opcode.Else);
            _openConditions[shared].Literal = required[shared];
            _openConditions[shared].HasElse = true;
            shared++;
        }
        CloseConditions(shared);
        for (int i = shared; i < required.Length; i++)
        {
            EmitConditionIf(required[i], isBranch: false);
            _openConditions.Add(new OpenCondition { Literal = required[i] });
        }
    }

    private void CloseConditions(int count = 0)
    {
        while (_openConditions.Count > count)
        {
            Emit(Opcode.Endif);
            _openConditions.RemoveAt(_openConditions.Count - 1);
        }
    }

    private void EmitConditionIf(Literal literal, bool isBranch)
    {
        _hasFlowAttribute.Add(isBranch);
        if (literal.Condition == null)
        {
            Emit(Opcode.IfC, (int)(literal.Negated ? IfComparison.EQ : IfComparison.NE), Zero(), Zero());
            return;
        }
        Emit(Opcode.IfC, (int)(literal.Negated ? IfComparison.EQ : IfComparison.NE),
            ConditionSource(literal.Condition), Zero());
    }

    // p0 == condition as one literal: constant where p0 is.
    private Literal PredicateLiteral(bool condition)
    {
        Literal[] p0 = KnownP0();
        if (IsNever(p0) || p0.Length == 0)
        {
            return new Literal(null, Negated: IsNever(p0) != condition);
        }
        if (p0.Length == 1)
        {
            return condition ? p0[0] : p0[0] with { Negated = !p0[0].Negated };
        }
        return Materialize(p0, negated: !condition);
    }

    private void EmitStructuredPredicateIf(bool condition, bool isBranch)
    {
        Literal literal = PredicateLiteral(condition);
        EmitConditionIf(literal, isBranch);
    }

    private void EmitStructuredPredicateBreak(bool condition)
    {
        if (!condition && KnownP0().Length > 1)
        {
            EmitBreakUnless(KnownP0());
            return;
        }
        Literal literal = PredicateLiteral(condition);
        if (literal.Condition == null)
        {
            if (literal.Negated)
            {
                Emit(Opcode.Break);
            }
            return;
        }
        Emit(Opcode.BreakC, (int)(literal.Negated ? IfComparison.EQ : IfComparison.NE),
            ConditionSource(literal.Condition), Zero());
    }

    // A break where p0 is not set. XNA's compiler turns a break on any test but
    // one flag the loop carries into a clear of a counter nothing reads, its loop
    // running on; so where the outermost condition holds, it takes the others'
    // product, and the break tests it alone. That changes it only for the pixels
    // breaking out: the counters holding it are not followed past the loop.
    private void EmitBreakUnless(Literal[] p0)
    {
        CloseConditions();
        if (IsNever(p0))
        {
            Emit(Opcode.Break);
        }
        else if (p0.Length > 1 && !p0[0].Negated && _loops.Count != 0)
        {
            Condition flag = p0[0].Condition;
            OpenConditions([p0[0]]);
            uint destination = Destination(RegisterType.Temp, flag.Register, 1 << flag.Lane);
            uint product = ConditionSource(flag);
            for (int i = 1; i < p0.Length; i++)
            {
                Condition condition = p0[i].Condition;
                if (i == 1)
                {
                    if (p0[i].Negated)
                    {
                        Emit(Opcode.Add, destination, ConditionSource(condition, SourceModifier.Negate), One());
                    }
                    else
                    {
                        Emit(Opcode.Mov, destination, ConditionSource(condition));
                    }
                }
                else if (p0[i].Negated)
                {
                    Emit(Opcode.Mad, destination, product, ConditionSource(condition, SourceModifier.Negate), product);
                }
                else
                {
                    Emit(Opcode.Mul, destination, product, ConditionSource(condition));
                }
            }
            CloseConditions();
            Emit(Opcode.BreakC, (int)IfComparison.EQ, ConditionSource(flag), Zero());
            _loops.Peek().Reassigned.Add(flag);
            _materializedP0 = null;
        }
        else
        {
            foreach (Literal literal in p0)
            {
                Emit(Opcode.BreakC, (int)(literal.Negated ? IfComparison.NE : IfComparison.EQ),
                    ConditionSource(literal.Condition), Zero());
            }
        }
        _breakEnd = _code.Count;
        _brokeUnless = p0;
    }

    // Whether an instruction only clears a counter, on !p0: a break unless p0.
    private static bool ClearsUnlessP0(AluInstruction alu, bool? condition) =>
        condition == false && alu.ScalarOpcode == AluScalarOpcode.SetpClr
            && alu.VectorWriteMask == 0 && !ChangesState(alu.VectorOpcode);

    // Whether an unpredicated instruction only moves the stack: it then runs
    // inside the ifs open, which its conditions are computed under.
    private bool IsStackOnly(AluInstruction alu)
    {
        bool vectorRuns = alu.VectorWriteMask != 0 || ChangesState(alu.VectorOpcode);
        bool scalarRuns = alu.ScalarWriteMask != 0 || ChangesState(alu.ScalarOpcode)
            || _usesPrevious && alu.ScalarOpcode != AluScalarOpcode.RetainPrev;
        bool vectorPush = IsPush(alu.VectorOpcode);
        bool scalarStack = alu.ScalarOpcode is AluScalarOpcode.SetpPop or AluScalarOpcode.SetpInv
            or AluScalarOpcode.SetpRstr;
        return !alu.IsExport && (vectorRuns || scalarRuns) && (!vectorRuns || vectorPush) && (!scalarRuns || scalarStack)
            && !(vectorRuns && scalarRuns);
    }

    private static bool IsPush(AluVectorOpcode opcode) =>
        opcode is >= AluVectorOpcode.SetpEqPush and <= AluVectorOpcode.SetpGePush;

    private static bool IsStackScalar(AluScalarOpcode opcode) =>
        opcode is AluScalarOpcode.SetpPop or AluScalarOpcode.SetpInv or AluScalarOpcode.SetpRstr
            or AluScalarOpcode.SetpClr;

    // Before an instruction: what reads a counter as a value cannot be followed,
    // nor what runs where the pixels that broke out of the loop still run.
    private void CheckCounterReads(AluInstruction alu, bool? condition)
    {
        _readCondition = condition;
        _zeroReads.Clear();
        bool vectorRuns = alu.VectorWriteMask != 0 || ChangesState(alu.VectorOpcode);
        bool scalarRuns = alu.ScalarWriteMask != 0 || ChangesState(alu.ScalarOpcode)
            || _usesPrevious && alu.ScalarOpcode != AluScalarOpcode.RetainPrev;
        if (_broken && condition != true)
        {
            // Broken out, p0 is 0: what runs on !p0 or always runs there too.
            bool clears = alu.ScalarOpcode == AluScalarOpcode.SetpClr && !vectorRuns;
            bool moves = condition == null && IsStackOnly(alu);
            if (!clears && !moves)
            {
                throw new UnstructuredPredicateException("An instruction runs where the loop was broken out of");
            }
        }
        if (vectorRuns)
        {
            int mask = alu.VectorWriteMask == 0 || alu.IsExport ? 0b1111 : alu.VectorWriteMask;
            for (int i = IsPush(alu.VectorOpcode) ? 2 : 1; i <= VectorOperandCount(alu.VectorOpcode); i++)
            {
                CheckNotCounter(alu, i, IsPush(alu.VectorOpcode)
                    ? (1 << VectorComponent(alu, i, 0)) | (1 << VectorComponent(alu, i, 3))
                    : VectorLanesRead(alu, i, mask));
            }
        }
        if (scalarRuns && !IsStackScalar(alu.ScalarOpcode))
        {
            switch (ScalarOperandForm(alu.ScalarOpcode))
            {
                case 1:
                    CheckNotCounter(alu, 3, 1 << ScalarA(alu));
                    break;
                case 2:
                    CheckNotCounter(alu, 3, (1 << ScalarA(alu)) | (1 << ScalarB(alu)));
                    break;
                case 3:
                    CheckNotCounter(alu.ScalarConstantOperationTemp, 1 << ScalarB(alu));
                    break;
            }
        }
    }

    private void CheckNotCounter(AluInstruction alu, int i, int lanes)
    {
        if (alu.SourceIsTemp(i))
        {
            CheckNotCounter(alu.SourceRegister(i) & 0x3F, lanes);
        }
    }

    // Run on p0 where its conditions are all the counter's, the counter is 0:
    // the compiler reads it as a zero there, which the lane is then made.
    private void CheckNotCounter(int register, int lanes)
    {
        for (int lane = 0; lane < 4; lane++)
        {
            if ((lanes & (1 << lane)) != 0 && _counters.TryGetValue((register, lane), out Literal[] stack))
            {
                if (_readCondition != true || _p0 == null || !Implies(_p0, stack))
                {
                    throw new UnstructuredPredicateException("A counter read as a value");
                }
                _zeroReads.Add((register, lane));
            }
        }
    }

    private void EmitZeroReads()
    {
        foreach ((int register, int lane) in _zeroReads)
        {
            Emit(Opcode.Mov, Destination(RegisterType.Temp, register, 1 << lane), Zero());
        }
        _zeroReads.Clear();
    }

    // After an instruction: what it wrote other than counters is no counter.
    private void ForgetWrittenCounters(AluInstruction alu)
    {
        if (alu.IsExport)
        {
            return;
        }
        if (!IsPush(alu.VectorOpcode))
        {
            ForgetCounters(alu.VectorDestination, alu.VectorWriteMask);
        }
        if (!IsStackScalar(alu.ScalarOpcode))
        {
            ForgetCounters(alu.ScalarDestination, alu.ScalarWriteMask);
        }
    }

    private void ForgetCounters(int register, int mask)
    {
        for (int lane = 0; lane < 4; lane++)
        {
            if ((mask & (1 << lane)) != 0)
            {
                _counters.Remove((register, lane));
            }
        }
    }

    private void CheckFetch(FetchInstruction fetch, bool? condition)
    {
        _readCondition = condition;
        _zeroReads.Clear();
        if (_broken && condition != true)
        {
            throw new UnstructuredPredicateException("A fetch runs where the loop was broken out of");
        }
        // A vertex fetch reads its index lane; a texture fetch at most three coordinates.
        CheckNotCounter(fetch.Source, fetch.Opcode == FetchOpcode.VertexFetch
            ? 1 << fetch.VertexSourceSwizzle : FetchLanes(fetch, 3));
    }

    // The lanes a fetch writes: its destination swizzle keeps those it gives 7.
    private static int FetchWrittenLanes(FetchInstruction fetch)
    {
        int lanes = 0;
        for (int i = 0; i < 4; i++)
        {
            if (((fetch.DestinationSwizzle >> (3 * i)) & 0b111) != 0b111)
            {
                lanes |= 1 << i;
            }
        }
        return lanes;
    }

    // The conditions on the counter an operand reads: a counter followed, or 0.
    private Literal[] CounterRead(AluInstruction alu, int i, int lane, out (int Register, int Lane)? key)
    {
        int register = alu.SourceRegister(i);
        if (alu.SourceIsTemp(i))
        {
            if ((register & 0x40) != 0 || alu.SourceNegate(i))
            {
                throw new UnstructuredPredicateException("A counter read through a modifier");
            }
            key = (register & 0x3F, lane);
            if (!_counters.TryGetValue(key.Value, out Literal[] stack))
            {
                throw new UnstructuredPredicateException("A counter that cannot be followed");
            }
            foreach (LoopState loop in _loops)
            {
                if (loop.Counters.TryGetValue(key.Value, out Literal[] start) && ReferenceEquals(start, stack))
                {
                    loop.CountersRead.Add(key.Value);
                }
            }
            return stack;
        }
        key = null;
        int index = (register - _shader.LiteralRegister) * 4 + lane;
        if (alu.IsSourceConstantRelative(i) || index < 0 || index >= _shader.Literals.Count
            || BitConverter.UInt32BitsToSingle(_shader.Literals[index]) != 0)
        {
            throw new UnstructuredPredicateException("A counter from a constant other than 0");
        }
        return [];
    }

    private void SetCounters(int register, int mask, Literal[] stack, (int Register, int Lane)? from)
    {
        bool broken = from != null && _brokenCounters.Contains(from.Value);
        for (int lane = 0; lane < 4; lane++)
        {
            if ((mask & (1 << lane)) != 0)
            {
                _counters[(register, lane)] = stack;
                if (broken)
                {
                    _brokenCounters.Add((register, lane));
                }
                else
                {
                    _brokenCounters.Remove((register, lane));
                }
            }
        }
    }

    private void CheckStackFrom((int Register, int Lane)? from)
    {
        if (_broken && _instructionCondition != true && (from == null || !_brokenCounters.Contains(from.Value)))
        {
            throw new UnstructuredPredicateException("p0 set where the loop was broken out of");
        }
    }

    // setp_*_push: the condition on top of the counter's.
    private void Push(AluInstruction alu, IfComparison comparison, int mask)
    {
        int counterLane = VectorComponent(alu, 1, 0);
        int conditionLane = VectorComponent(alu, 2, 0);
        if (alu.IsExport || VectorComponent(alu, 1, 3) != counterLane || VectorComponent(alu, 2, 3) != conditionLane)
        {
            throw new UnstructuredPredicateException("A push other than of one lane");
        }
        if (_instructionCondition != null)
        {
            throw new UnstructuredPredicateException("A predicated push");
        }
        Literal[] stack = CounterRead(alu, 1, counterLane, out var from);
        CheckStackFrom(from);
        // The condition matters only where those under it hold: inside ifs on
        // some of them, it is computed there.
        if (_openConditions.Count > stack.Length
            || !_openConditions.Select(open => open.Literal).SequenceEqual(stack.Take(_openConditions.Count)))
        {
            CloseConditions();
        }
        Condition condition = NewCondition();
        EmitComparison(Destination(RegisterType.Temp, condition.Register, 1 << condition.Lane),
            AluSource(alu, 2, Replicate(conditionLane)), comparison);
        Literal[] pushed = [.. stack, new Literal(condition, false)];
        SetCounters(alu.VectorDestination, mask, pushed, from);
        SetP0(pushed);
    }

    // setp_pop, setp_inv, setp_rstr and setp_clr.
    private void MoveStack(AluInstruction alu, int mask)
    {
        if (alu.IsExport)
        {
            throw new UnstructuredPredicateException("A counter exported");
        }
        if (alu.ScalarOpcode == AluScalarOpcode.SetpClr)
        {
            Clear(alu, mask);
            return;
        }
        if (_instructionCondition != null)
        {
            throw new UnstructuredPredicateException("A predicated stack operation");
        }
        Literal[] stack = CounterRead(alu, 3, ScalarA(alu), out var from);
        CheckStackFrom(from);
        Literal[] result;
        switch (alu.ScalarOpcode)
        {
            case AluScalarOpcode.SetpPop:
                result = stack.Length == 0 ? stack : stack[..^1];
                break;
            case AluScalarOpcode.SetpInv:
                if (stack.Length == 0)
                {
                    throw new UnstructuredPredicateException("An inversion of no condition");
                }
                result = [.. stack[..^1], stack[^1] with { Negated = !stack[^1].Negated }];
                break;
            default:
                result = stack;
                break;
        }
        SetCounters(alu.ScalarDestination, mask, result, from);
        SetP0(result);
    }

    // setp_clr in a loop: where it runs, the pixel breaks out.
    private void Clear(AluInstruction alu, int mask)
    {
        if (_loops.Count == 0 || mask == 0)
        {
            throw new UnstructuredPredicateException("A clear other than a loop's break");
        }
        if (_instructionCondition == false)
        {
            EmitBreakUnless(KnownP0());
        }
        else
        {
            Emit(Opcode.Break);
            _breakEnd = _code.Count;
            _brokeUnless = null;
        }
        _broken = true;
        for (int lane = 0; lane < 4; lane++)
        {
            if ((mask & (1 << lane)) != 0)
            {
                _brokenCounters.Add((alu.ScalarDestination, lane));
            }
        }
        // The pixels left are those it did not run on: on p0 it was 0 for them.
        if (_instructionCondition != false)
        {
            SetP0(Never);
        }
    }

    // setp_eq/ne/gt/ge: p0 the comparison, under p0 where predicated on it.
    private void SetPredicateComparison(uint[] value, IfComparison comparison)
    {
        if (_instructionCondition == false || _broken && _instructionCondition == null)
        {
            throw new UnstructuredPredicateException("p0 set where it is 0");
        }
        if (_instructionCondition == null)
        {
            CloseConditions();
        }
        Condition condition = NewCondition();
        EmitComparison(Destination(RegisterType.Temp, condition.Register, 1 << condition.Lane), value, comparison);
        Literal[] p0 = _instructionCondition == true ? KnownP0() : [];
        SetP0([.. p0, new Literal(condition, false)]);
    }

    private PredicateState CapturePredicate() => new(new Dictionary<(int, int), Literal[]>(_counters), _p0);

    private void RestorePredicate(PredicateState state)
    {
        _counters.Clear();
        foreach (var counter in state.Counters)
        {
            _counters.Add(counter.Key, counter.Value);
        }
        SetP0(state.P0);
    }

    // Where two paths join: what they agree on is still known.
    private void JoinPredicate(PredicateState other)
    {
        foreach (var counter in _counters.ToList())
        {
            if (!other.Counters.TryGetValue(counter.Key, out Literal[] stack) || !stack.SequenceEqual(counter.Value))
            {
                _counters.Remove(counter.Key);
            }
        }
        if (_p0 == null || other.P0 == null || !_p0.SequenceEqual(other.P0))
        {
            SetP0(null);
        }
    }

    // Control flow other than an exec: what was computed for p0 holds in its block only.
    private void EnterControlFlow()
    {
        if (_broken)
        {
            throw new UnstructuredPredicateException("Control flow where the loop was broken out of");
        }
        _materializedP0 = null;
    }

    private void StartLoopPredicate()
    {
        EnterControlFlow();
        _loops.Push(new LoopState { Counters = new Dictionary<(int, int), Literal[]>(_counters), P0 = _p0 });
    }

    // Whether p0 implies all the conditions of another.
    private static bool Implies(Literal[] p0, Literal[] other) =>
        IsNever(p0) || !IsNever(other) && other.All(p0.Contains);

    // Whether the endloop's break, where p0 is not set, repeats one just made so.
    private bool RepeatsBreak(ControlFlowInstruction cf) =>
        cf.IsPredicatedBreak && !cf.Condition && _breakEnd == _code.Count
            && _brokeUnless != null && ReferenceEquals(_brokeUnless, _p0);

    private void EndLoopPredicate(ControlFlowInstruction cf)
    {
        LoopState loop = _loops.Pop();
        foreach (var counter in loop.Counters)
        {
            if (loop.CountersRead.Contains(counter.Key)
                && (!_counters.TryGetValue(counter.Key, out Literal[] stack) || !stack.SequenceEqual(counter.Value)))
            {
                throw new UnstructuredPredicateException("A loop that ends with other counters than it starts");
            }
        }
        // Going round again where p0 is 0 breaking out, p0 holds there: the
        // conditions it started on still hold where all those it ends on do.
        bool p0Holds = _p0 != null && loop.P0 != null && (cf.IsPredicatedBreak && !cf.Condition
            ? Implies(_p0, loop.P0) : _p0.SequenceEqual(loop.P0));
        if (loop.P0Read && !p0Holds)
        {
            throw new UnstructuredPredicateException("A loop that ends with another p0 than it starts");
        }
        if (_broken)
        {
            // The pixels that broke out leave here: p0 is 0 for them.
            if (!cf.IsPredicatedBreak || cf.Condition)
            {
                throw new UnstructuredPredicateException("A loop broken out of that loops on");
            }
            foreach (var counter in _brokenCounters)
            {
                _counters.Remove(counter);
            }
            _brokenCounters.Clear();
            _broken = false;
            SetP0(null);
        }
        foreach (var counter in _counters.Where(counter =>
            counter.Value.Any(literal => loop.Reassigned.Contains(literal.Condition))).ToList())
        {
            _counters.Remove(counter.Key);
        }
        _materializedP0 = null;
    }
}
