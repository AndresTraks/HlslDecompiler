using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Recovers an idiom into the value graph, after the templates have folded a group
/// and before anything has decided what to name - which is the one thing the
/// groupers cannot do.
///
/// A template rewrites the graph: DotProduct2Template turns `a*x + b*y` into a
/// DotProductOperation, and from then on everything downstream sees a dot product,
/// because there is one in the graph. A grouper does not. It is asked, while a
/// statement is being compiled, whether the components about to be written happen to
/// spell out a normalize or a matrix multiply, and it answers for that write and
/// nothing else. So the naming that runs in between sees neither: it measures a
/// statement by compiling it, which is the only way to find out what a grouper would
/// claim, and the measurement is stale as soon as it has named something, because
/// naming changes what the groupers match. ps_5_0/split_transform is two
/// instructions of exactly that, and the entry on it in RoundTripCostTests records
/// three rules that do not fix it.
///
/// The difference is not in what the groupers know. NormalizeGrouper's test is local
/// to the nodes - a length, the divisions by it, and whether the dividends are the
/// vector it measured - and asks nothing about how the writer grouped anything. It
/// runs late because that is where it was put, and a normalize recovered here is a
/// node the way a dot product is: nameable as one thing, visible to every pass
/// after, and impossible to take half of.
///
/// What it is worth, measured over the corpus with the sweep: eight shaders change,
/// every one recompiles, stays equivalent on the interpreter, and costs not one
/// instruction more.
///
/// Five of them stop writing a normalize twice. ps_4_1/cube_array_probe wrote
/// `normalize(i.normal)` and `normalize(i.texcoord1)` once in a dot and again in a
/// reflect; each is named once now. That is the thing CostsAnInstruction is written
/// about - a normalize written twice is computed twice - and it could not be acted
/// on while the idiom existed only inside a compile.
///
/// ps_4_0/packed_cbuffer is the other half of it, and shows how far this gets and
/// no further. That shader normalizes a vector and reads its .xz and its .y apart,
/// so the grouper only ever saw two components of three and matched nothing: the
/// output took a length, divided .xz by it and divided .y by it again, and read the
/// result as `float3(t6.x, t4.y / t5, t6.y)`. Asked of the graph, where all three
/// divisions are, the normalize comes back whole - `float3 t5 = normalize(t4)` with
/// `float2 t6 = t5.xz` for the two components two samples want. The constructor
/// repacking them is still there, reading `float3(t6.x, t5.y, t6.y)`, and three
/// lines are still three lines. What changed is that the idiom is named rather than
/// spelled out; putting the swizzles back together is a separate question and not
/// one this answers.
///
/// ps_4_0/reflect_cube is the one that reads longer: two normalizes that one
/// expression each reads get names, and a line of a hundred characters becomes
/// three short ones. That is the hoist naming an expensive read with a single
/// reader, which is what it is documented to do and what keeps the fixtures short.
///
/// reflect followed, and the first attempt at it says something about the order
/// these have to go in. Tried while the naming still counted the expressions that
/// read a value - by walking every reader and dropping the ones CanGroupComponents
/// said could be components of one value - it fixed reflect_cube and lost
/// ps_4_1/cube_array_probe a name it wanted: that shader reads
/// `normalize(i.normal)` in a fresnel dot and again in the reflect, and once the
/// reflect was a node the two readers were both GroupNodes holding the same
/// components, which that count collapsed into one. Measured then: the count came
/// back 1 for that normalize and 2 for the one beside it that a dot and a negate
/// read.
///
/// So it waited for the count to become what it is now - how many times the
/// compiler wrote the value, which the measurement has always had - and with that
/// in place the same recovery moves three shaders, costs no instruction, and makes
/// two of their longest lines shorter and none longer. An idiom cannot be moved
/// into the graph ahead of the naming being able to count what the move changes.
///
/// Where this runs matters and is narrow. The templates fold a register group in
/// GroupAssignments, immediately before the hoist names anything in it, and that is
/// the only point where the pattern exists and nothing has been named yet. Earlier
/// than that - before WriteStatements, say - the graph is still raw instructions:
/// ReduceAll folds only what the writer folds before writing, deliberately, so that
/// what the groupers match reaches them as the instructions still are.
/// </summary>
public static class IdiomRecovery
{
    /// <summary>
    /// Recovers what it can and patches the groups, since a group's own entry may be
    /// the node that was replaced: Replace relinks the readers of a node, and a root
    /// has none.
    /// </summary>
    public static void Recover(IList<HlslTreeNode[]> groups)
    {
        var replaced = new Dictionary<HlslTreeNode, HlslTreeNode>(
            ReferenceEqualityComparer.Instance);
        // Which component of its register each root is, so that a normalize keeps
        // the order it was written in: `normalize(position.yxz)` divides position.y
        // into .x, and ordering its components by the vector the length was taken
        // over instead writes `normalize(position.xyz).yxz` - the same value, read
        // as the swizzle of a different vector.
        var componentOf = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        foreach (HlslTreeNode[] group in groups)
        {
            for (int i = 0; i < group.Length; i++)
            {
                componentOf.TryAdd(group[i], i);
            }
        }
        // Collected before any rewriting: a rewrite relinks the readers of what it
        // replaces, and walking the graph while that happens reads a node's inputs
        // as they are being moved.
        List<HlslTreeNode> all = [.. Reachable(groups.SelectMany(group => group))];
        // Reflects before normalizes, since a reflect's operands are often
        // normalizes and the decomposition reads them either way, while the dot it
        // is anchored on is easier to find before anything under it has moved.
        foreach (DotProductOperation dot in all.OfType<DotProductOperation>().ToList())
        {
            RecoverReflect(dot, replaced);
        }
        foreach (LengthOperation length in all.OfType<LengthOperation>().ToList())
        {
            RecoverNormalize(length, componentOf, replaced);
        }
        if (replaced.Count == 0)
        {
            return;
        }
        foreach (HlslTreeNode[] group in groups)
        {
            for (int i = 0; i < group.Length; i++)
            {
                if (replaced.TryGetValue(group[i], out HlslTreeNode replacement))
                {
                    group[i] = replacement;
                }
            }
        }
    }

    /// <summary>
    /// `i - 2 * dot(i, n) * n`, which is what fxc writes for reflect(i, n): the dot,
    /// an add of it to itself or a multiply by two, and a mad per component with the
    /// scale negated.
    ///
    /// Driven from the dot outwards, where ReflectGrouper is driven from the
    /// components inwards. The dot is the one part of the shape that is a single
    /// node, so it is the only part a walk of the graph can find without being told
    /// which components to look at.
    /// </summary>
    private static void RecoverReflect(
        DotProductOperation dot, Dictionary<HlslTreeNode, HlslTreeNode> replaced)
    {
        foreach (HlslTreeNode scale in Scales(dot))
        {
            // Every multiply that scales something by the negated scale, from all of
            // the negations of it rather than from one. The graph holds a value a
            // component at a time, so `-2 * dot(i, n)` broadcast over three
            // components is three NegateOperation nodes with a multiply each, and
            // looking for all three components among the readers of one of them
            // finds exactly one.
            List<MultiplyOperation> scaled = [.. scale.Outputs
                .Distinct(ReferenceEqualityComparer.Instance)
                .OfType<NegateOperation>()
                .SelectMany(negated => negated.Outputs
                    .Distinct(ReferenceEqualityComparer.Instance)
                    .OfType<MultiplyOperation>()
                    .Where(multiply => ReferenceEquals(multiply.Factor1, negated)
                        || ReferenceEquals(multiply.Factor2, negated)))
                .Distinct(ReferenceEqualityComparer.Instance)
                .Cast<MultiplyOperation>()];
            if (scaled.Count == 0)
            {
                continue;
            }
            // Either side may be the normal: a dot does not say which of its
            // operands the reflect scaled.
            if (TryRecoverReflect(dot.X, dot.Y, scaled, replaced)
                || TryRecoverReflect(dot.Y, dot.X, scaled, replaced))
            {
                return;
            }
        }
    }

    /// <summary>
    /// How the scale reaches the components: twice the dot, as the multiply the
    /// source said or as the add of it to itself that fxc emits before
    /// AddSelfTemplate has been at it.
    /// </summary>
    private static IEnumerable<HlslTreeNode> Scales(DotProductOperation dot)
    {
        foreach (HlslTreeNode reader in dot.Outputs.Distinct(ReferenceEqualityComparer.Instance))
        {
            if (reader is AddOperation add && ReferenceEquals(add.Addend1, add.Addend2))
            {
                yield return add;
            }
            else if (reader is MultiplyOperation multiply
                && (IsTwo(multiply.Factor1) || IsTwo(multiply.Factor2)))
            {
                yield return multiply;
            }
        }
    }

    private static bool IsTwo(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.Value == 2f;
    }

    private static bool TryRecoverReflect(
        GroupNode incident,
        GroupNode normal,
        List<MultiplyOperation> scaled,
        Dictionary<HlslTreeNode, HlslTreeNode> replaced)
    {
        if (incident.Inputs.Count != normal.Inputs.Count || incident.Inputs.Count < 2)
        {
            return false;
        }
        // One component at a time, each from the mad that scaled this component of
        // the normal and added this component of the incident. Positionally, the way
        // ReflectGrouper matches them: it compares the components it was handed with
        // the dot's own inputs in order, so a reflect whose sides are in a different
        // order from the write is not one it recognises either.
        var components = new HlslTreeNode[incident.Inputs.Count];
        for (int i = 0; i < components.Length; i++)
        {
            components[i] = FindReflectComponent(
                incident.Inputs[i], normal.Inputs[i], scaled);
            if (components[i] == null)
            {
                return false;
            }
        }
        if (components.Distinct(ReferenceEqualityComparer.Instance).Count() != components.Length)
        {
            return false;
        }
        for (int i = 0; i < components.Length; i++)
        {
            var reflected = new ReflectOutputNode(incident, normal, i);
            components[i].Replace(reflected);
            replaced[components[i]] = reflected;
        }
        return true;
    }

    /// <summary>
    /// The add or subtract that is one component of the reflection: this component
    /// of the normal times the negated scale, plus this component of the incident.
    /// </summary>
    private static HlslTreeNode FindReflectComponent(
        HlslTreeNode incident, HlslTreeNode normal, List<MultiplyOperation> scaled)
    {
        foreach (MultiplyOperation multiply in scaled)
        {
            HlslTreeNode other = multiply.Factor1 is NegateOperation
                ? multiply.Factor2
                : multiply.Factor1;
            if (!NodeGrouper.AreNodesEquivalent(other, normal))
            {
                continue;
            }
            foreach (HlslTreeNode reader in multiply.Outputs
                .Distinct(ReferenceEqualityComparer.Instance))
            {
                if (reader is AddOperation add
                    && NodeGrouper.AreNodesEquivalent(Other(add, multiply), incident))
                {
                    return add;
                }
                // `reflect(-x, n)` is `-x - 2*dot(-x,n)*n`, which fxc writes as
                // `n * -scale - x`: the scaled multiply is the minuend, what is
                // taken off it is x, and the incident the dot holds is -x. So the
                // subtraction is how a component says its incident is a negation,
                // and the negation is on the dot's side rather than in the line.
                if (reader is SubtractOperation subtract
                    && ReferenceEquals(subtract.Minuend, multiply)
                    && incident is NegateOperation negatedIncident
                    && NodeGrouper.AreNodesEquivalent(subtract.Subtrahend, negatedIncident.Value))
                {
                    return subtract;
                }
            }
        }
        return null;
    }

    private static HlslTreeNode Other(AddOperation add, HlslTreeNode addend)
    {
        return ReferenceEquals(add.Addend1, addend) ? add.Addend2 : add.Addend1;
    }

    /// <summary>
    /// The divisions by one length, where they divide the whole of what the length
    /// was taken over: that is a normalize, one component at a time.
    ///
    /// The whole of it and not part - the same condition NormalizeGrouper applies,
    /// and for the same reason. A parallax shader normalizes a tangent space view
    /// vector and reads its .xy and its .z apart, and two of the three components on
    /// their own are not a normalize of those two. What differs here is that the
    /// question is asked of the graph, where all three divisions are, rather than of
    /// the components one write happens to cover - so the shader that reads them
    /// apart gets its normalize back, and reads them out of it.
    /// </summary>
    private static void RecoverNormalize(
        LengthOperation length,
        Dictionary<HlslTreeNode, int> componentOf,
        Dictionary<HlslTreeNode, HlslTreeNode> replaced)
    {
        if (length.X is not GroupNode vector || vector.Inputs.Count < 2)
        {
            return;
        }
        List<HlslTreeNode> measured = [.. vector.Inputs];
        List<DivisionOperation> divisions = [.. length.Outputs
            .Distinct(ReferenceEqualityComparer.Instance)
            .OfType<DivisionOperation>()
            .Where(division => ReferenceEquals(division.Divisor, length))];

        // Which component of the normalize each division computes, matched the way
        // the grouper matches them: by equivalence and in any order, since a length
        // does not care which way round its vector is.
        //
        // More divisions than components is the ordinary case and not a reason to
        // stop: the bytecode divides once per use as often as not, so a normalize
        // read by an output and by three dots arrives as four divisions over three
        // components. They share one node per component here, which is also the only
        // honest reading of them - a normalize written twice is computed twice, and
        // fxc folded `normalize(t)` read by a mad and a cross into three
        // instructions rather than the one it came from.
        var component = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        var ordered = new HlslTreeNode[measured.Count];

        // The written order first, where the divisions are the components of a
        // register being written: that order is the vector the shader normalized,
        // and the length's own order says nothing about it.
        List<DivisionOperation> written = [.. divisions
            .Where(componentOf.ContainsKey)
            .OrderBy(division => componentOf[division])];
        if (written.Count == measured.Count)
        {
            for (int i = 0; i < written.Count; i++)
            {
                component[written[i]] = i;
                ordered[i] = written[i].Dividend;
            }
        }

        // Otherwise the length's own order, one component each: a component is
        // claimed as it is placed, because AreNodesEquivalent is a question about
        // shape and several components of one vector answer it alike. Searching from
        // the start each time put all three dividends of a tangent space normalize
        // on component 0 and left the other two unplaced.
        List<int> unplaced = [.. Enumerable.Range(0, measured.Count)];
        var waiting = new List<DivisionOperation>();
        foreach (DivisionOperation division in divisions.Where(d => !component.ContainsKey(d)))
        {
            int at = unplaced.FindIndex(
                index => NodeGrouper.AreNodesEquivalent(division.Dividend, measured[index]));
            if (at < 0)
            {
                waiting.Add(division);
                continue;
            }
            int index = unplaced[at];
            unplaced.RemoveAt(at);
            component[division] = index;
            // The dividend rather than what the length holds, which is what the
            // grouper compiles: the two are equivalent, and the dividends are the
            // ones the divisions were reading.
            ordered[index] ??= division.Dividend;
        }

        // And what is left over goes onto a component already placed, where it
        // computes the same one. This is where a duplicate lands: the bytecode
        // divides once per use as often as not, so a normalize read by an output and
        // by three dots arrives as four divisions over three components, and they
        // share one node per component here. A division by the length that computes
        // no component at all is not part of the normalize and is left alone - the
        // reciprocal is always one of those, since the bytecode divides one by the
        // length and multiplies each component by that, and
        // MultiplyReciprocalDivisionTemplate rewrites the multiplies into divisions
        // while the `1 / length` it came from stays where it was.
        foreach (DivisionOperation division in waiting)
        {
            int index = System.Array.FindIndex(ordered,
                c => c != null && NodeGrouper.AreNodesEquivalent(division.Dividend, c));
            if (index >= 0)
            {
                component[division] = index;
            }
        }

        // The whole of what the length was taken over, and not part of it - the same
        // condition NormalizeGrouper applies, and for the same reason. Part is not a
        // normalize of that part: a parallax shader normalizes a tangent space view
        // vector and reads its .xy and its .z apart, and those two components alone
        // are not normalize(v.xy), they are normalize(v).xy. What differs here is
        // that the question is asked of the graph, where all three divisions are,
        // rather than of the components one write happens to cover - so a shader
        // that reads them apart keeps its normalize and swizzles out of it.
        if (ordered.Any(dividend => dividend == null))
        {
            return;
        }

        var normalized = new NormalizeOutputNode[measured.Count];
        foreach (DivisionOperation division in divisions.Where(component.ContainsKey))
        {
            int index = component[division];
            normalized[index] ??= new NormalizeOutputNode(ordered, index);
            division.Replace(normalized[index]);
            replaced[division] = normalized[index];
        }
    }

    private static IEnumerable<HlslTreeNode> Reachable(IEnumerable<HlslTreeNode> roots)
    {
        var seen = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>(roots);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (!seen.Add(node))
            {
                continue;
            }
            yield return node;
            foreach (HlslTreeNode input in node.Inputs)
            {
                pending.Push(input);
            }
        }
    }
}
