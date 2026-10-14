using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Recovers reflect, matrix multiply and normalize into the value graph as single
/// nodes, so naming sees each idiom whole instead of naming half of one.
/// Runs in GroupAssignments after the templates fold and before the hoist names
/// anything; earlier the graph is still the raw shape the groupers match.
/// </summary>
public static class IdiomRecovery
{
    /// <summary>
    /// Patches the groups too: Replace relinks a node's readers, and a root has none.
    /// </summary>
    public static void Recover(
        IList<HlslTreeNode[]> groups, MatrixMultiplicationGrouper matrices)
    {
        var replaced = new Dictionary<HlslTreeNode, HlslTreeNode>(
            ReferenceEqualityComparer.Instance);
        // Written component order, so `normalize(p.yxz)` does not come back as
        // `normalize(p.xyz).yxz`.
        var componentOf = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        foreach (HlslTreeNode[] group in groups)
        {
            for (int i = 0; i < group.Length; i++)
            {
                componentOf.TryAdd(group[i], i);
            }
        }
        // Collected before rewriting, which relinks inputs mid-walk.
        List<HlslTreeNode> all = [.. Reachable(groups.SelectMany(group => group))];
        // Reflects first: their anchoring dot is easiest to find before anything moves.
        foreach (DotProductOperation dot in all.OfType<DotProductOperation>().ToList())
        {
            RecoverReflect(dot, replaced);
        }
        RecoverMatrixMultiplications(all, groups, matrices, replaced);
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
    /// Dot products that are rows of one matrix multiply, recovered into its
    /// components. Matching is delegated to MatrixMultiplicationGrouper; the dots are
    /// handed over in row order because it checks the dot at position i against row i.
    /// </summary>
    private static void RecoverMatrixMultiplications(
        List<HlslTreeNode> all,
        IList<HlslTreeNode[]> groups,
        MatrixMultiplicationGrouper matrices,
        Dictionary<HlslTreeNode, HlslTreeNode> replaced)
    {
        List<DotProductOperation> dots = [.. all.OfType<DotProductOperation>()];
        var taken = HlslTreeNode.NewNodeSet();
        foreach (DotProductOperation dot in dots)
        {
            if (taken.Contains(dot))
            {
                continue;
            }
            List<DotProductOperation> ordered = [.. dots
                .Where(other => !taken.Contains(other)
                    && (ReferenceEquals(other, dot) || matrices.AreRowsOfOneMatrix(dot, other)))
                .Select(other => (Dot: other, Row: matrices.MatrixRowRegister(other)))
                .Where(other => other.Row != null)
                .OrderBy(other => other.Row)
                .Select(other => other.Dot)];
            if (ordered.Count < 2)
            {
                continue;
            }

            // The w column form first: `mul(float4(p, 1), M)` folds to three-wide
            // dots plus the last column, so the components are the adds above them.
            if (TryRecover(WColumnAdds(ordered), groups, matrices, taken, replaced))
            {
                // Else the walk recovers a second, unread multiply from the dots.
                foreach (DotProductOperation taken_ in ordered)
                {
                    taken.Add(taken_);
                }
                continue;
            }
            TryRecover([.. ordered], groups, matrices, taken, replaced);
        }
    }

    /// <summary>
    /// The single add above each dot (a multiply with a w column), or null.
    /// </summary>
    private static HlslTreeNode[] WColumnAdds(List<DotProductOperation> ordered)
    {
        var adds = new HlslTreeNode[ordered.Count];
        for (int i = 0; i < ordered.Count; i++)
        {
            List<AddOperation> readers = [.. ordered[i].Outputs
                .Distinct(ReferenceEqualityComparer.Instance)
                .OfType<AddOperation>()];
            if (readers.Count != 1)
            {
                return null;
            }
            adds[i] = readers[0];
        }
        return adds;
    }

    /// <summary>
    /// Whether the components are a contiguous run of one write, in order. A multiply
    /// gathered from several registers splits its consumers apart (a vector divide
    /// by w, a nested `mul(mul(p, A), B)`).
    /// </summary>
    private static bool IsRunOfOneRegister(HlslTreeNode[] components, IList<HlslTreeNode[]> groups)
    {
        return groups
            .Where(group => group.Length >= components.Length)
            .Any(group => Enumerable
                .Range(0, group.Length - components.Length + 1)
                .Any(at => components
                    .Select((component, i) => ReferenceEquals(group[at + i], component))
                    .All(same => same)));
    }

    private static bool TryRecover(
        HlslTreeNode[] components,
        IList<HlslTreeNode[]> groups,
        MatrixMultiplicationGrouper matrices,
        HashSet<HlslTreeNode> taken,
        Dictionary<HlslTreeNode, HlslTreeNode> replaced)
    {
        if (components == null
            || components.Distinct(ReferenceEqualityComparer.Instance).Count() != components.Length
            || !IsRunOfOneRegister(components, groups))
        {
            return false;
        }
        MatrixMultiplicationContext matrix = matrices.TryGetMultiplicationGroup(components);
        // All rows: three of four rows is a different multiplication.
        if (matrix == null || matrix.MatrixRowCount != components.Length)
        {
            return false;
        }
        for (int i = 0; i < components.Length; i++)
        {
            var multiplied = new MatrixMultiplyOutputNode(matrix, i);
            components[i].Replace(multiplied);
            replaced[components[i]] = multiplied;
            taken.Add(components[i]);
        }
        return true;
    }

    /// <summary>
    /// fxc's reflect(i, n): the dot, doubled, and a mad per component with the scale
    /// negated. Driven from the dot, the only single-node part of the shape.
    /// </summary>
    private static void RecoverReflect(
        DotProductOperation dot, Dictionary<HlslTreeNode, HlslTreeNode> replaced)
    {
        foreach (HlslTreeNode scale in Scales(dot))
        {
            // From every negation of the scale: a broadcast `-2 * dot` is one
            // NegateOperation per component.
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
            // Either dot operand may be the normal.
            if (TryRecoverReflect(dot.X, dot.Y, scaled, replaced)
                || TryRecoverReflect(dot.Y, dot.X, scaled, replaced))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Twice the dot, as `2 * dot` or as `dot + dot` (before AddSelfTemplate).
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
        // Positionally against the dot's inputs, as ReflectGrouper matches.
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
    /// The add or subtract computing `normal * -scale + incident` for one component.
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
                // fxc writes `reflect(-x, n)` as `n * -scale - x`: the dot holds -x.
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
    /// Divisions by one length covering the whole vector it measures: a normalize.
    /// Asked of the graph, not one write, so components read apart still recover.
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

        // Which component each division computes, by equivalence in any order.
        var component = new Dictionary<HlslTreeNode, int>(ReferenceEqualityComparer.Instance);
        var ordered = new HlslTreeNode[measured.Count];

        // Written order first, where the divisions are one register's components.
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

        // Otherwise the length's order. A component is claimed once placed, since
        // AreNodesEquivalent compares shape and sibling components match alike.
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
            ordered[index] ??= division.Dividend;
        }

        // Duplicates (fxc divides once per use) share an already placed component.
        // A division matching none, like the leftover `1 / length`, is left alone.
        foreach (DivisionOperation division in waiting)
        {
            int index = System.Array.FindIndex(ordered,
                c => c != null && NodeGrouper.AreNodesEquivalent(division.Dividend, c));
            if (index >= 0)
            {
                component[division] = index;
            }
        }

        // All of the vector: two components alone are normalize(v).xy, not
        // normalize(v.xy).
        if (ordered.Any(dividend => dividend == null))
        {
            return;
        }

        // Not where the length has other readers: normalize(d) beside length(d)
        // costs an rsq and a sqrt instead of one sqrt. `1 / length` is not a reader.
        if (length.Outputs.Distinct(ReferenceEqualityComparer.Instance)
            .Any(reader => !(reader is DivisionOperation division && ReferenceEquals(division.Divisor, length)
                && (component.ContainsKey(division) || division.Dividend is ConstantNode { Value: 1 }))))
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
