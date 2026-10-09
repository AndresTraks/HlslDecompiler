using HlslDecompiler.Hlsl.FlowControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.TemplateMatch;

public class TemplateMatcher
{
    /// <summary>
    /// The templates, in the order they are tried at a node: every template of a
    /// stage before any of the next, and within a stage in the order listed. The
    /// first that matches rewrites the node, and the rewrite is reduced again from
    /// the first stage - so the order is a priority at one node and nothing more,
    /// and it is the priority each stage's comment gives the reason for.
    /// </summary>
    private readonly IReadOnlyList<Stage> _stages;

    /// <summary>A named run of templates, tried together.</summary>
    private sealed record Stage(string Name, IReadOnlyList<IStageTemplate> Templates);

    /// <summary>
    /// A template as a stage tries it: the node rewritten, or null where it does not
    /// match. A node template answers about the node; a group template about the
    /// node and what it found it grouped with.
    /// </summary>
    private interface IStageTemplate
    {
        string Name { get; }
        HlslTreeNode TryReduce(HlslTreeNode node);
    }

    private sealed class NodeStageTemplate(INodeTemplate template) : IStageTemplate
    {
        public string Name => template.GetType().Name;

        public HlslTreeNode TryReduce(HlslTreeNode node) =>
            template.Match(node) ? template.Reduce(node) : null;
    }

    private sealed class GroupStageTemplate(IGroupTemplate template) : IStageTemplate
    {
        public string Name => template.GetType().Name;

        public HlslTreeNode TryReduce(HlslTreeNode node) =>
            template.Match(node) is IGroupContext context ? template.Reduce(node, context) : null;
    }

    private static Stage NodeStage(string name, params INodeTemplate[] templates) =>
        new(name, [.. templates.Select(t => (IStageTemplate)new NodeStageTemplate(t))]);

    private static Stage GroupStage(string name, params IGroupTemplate[] templates) =>
        new(name, [.. templates.Select(t => (IStageTemplate)new GroupStageTemplate(t))]);

    private NodeGrouper _nodeGrouper;

    /// <summary>
    /// What a template may look through a variable to. A variable is a leaf to a
    /// template as a register is, and a fold that wants the loop-invariant expression
    /// behind one asks this for it. Null until the writer has looked at the function
    /// and built it.
    /// </summary>
    public TempResolver TempResolver { get; set; }

    public TemplateMatcher(NodeGrouper nodeGrouper)
    {
        _stages =
        [
            // Arithmetic identities: constants folded, negations moved onto what they
            // negate, moves and mads opened up, a reciprocal turned into the division
            // or the rsqrt it was. First, so that everything after sees one spelling
            // of a value - a constant on the side it is put on, no move in the way.
            NodeStage("Arithmetic identities",
                new AddConstantsTemplate(),
                new AddNegateTemplate(),
                new AddNegativeTemplate(),
                new AddSelfTemplate(),
                new AddZeroTemplate(),
                new MoveTemplate(),
                new MultiplyAddTemplate(),
                new MultiplyConstantsTemplate(),
                new MultiplyConstantTemplate(),
                new MultiplyNegativeOneTemplate(),
                new MultiplyNegatedTemplate(),
                new MultiplyOneTemplate(),
                new MultiplyReciprocalDivisionTemplate(),
                new MultiplyZeroTemplate(),
                new AbsoluteConstantTemplate(),
                new NegateConstantTemplate(),
                new NegateNegateTemplate(),
                new ReciprocalReciprocalSquareRootTemplate(),
                new ReciprocalSquareRootTemplate(),
                new SubtractNegateTemplate(),
                new SubtractZeroTemplate()),
            // NegateSubtractTemplate, `-(a - b)` as `b - a`, is not in it, and has
            // not been since 7873807 left it out without saying why.

            // Comparisons and selects: a cmp or a comparison put the way round the
            // source wrote it, and a select of constants folded into the test it
            // repeats. Before the intrinsics, which recognise sign, step and the
            // rest by the comparisons they are built of, in this form.
            NodeStage("Comparisons and selects",
                new CompareConstantTemplate(),
                new CompareNegativeWithZeroTemplate(),
                new CompareAbsoluteWithZeroTemplate(),
                new AddMaskTemplate(),
                new CompareSelectedConstantTemplate(),
                new ConvertSelectedConstantTemplate(),
                new ComparePositiveAndNegativeTemplate(),
                new CompareCompareTemplate()),

            // Intrinsics and idioms: the call or the expression a run of
            // instructions was compiled from - abs, sign, floor, pow, lerp,
            // smoothstep, clamp, an integer vector read at a computed component.
            // Each matches one value, and none claims another value's components.
            NodeStage("Intrinsics and idioms",
                new MaxOfPositiveAndNegativeTemplate(),
                new TrigonometricRangeReductionTemplate(),
                new SignedDivideTemplate(),
                new SignTemplate(),
                new FloorTemplate(),
                new TruncateTemplate(),
                new GuardedReciprocalTemplate(),
                new PowerTemplate(),
                new NaturalExponentialTemplate(),
                new NaturalLogarithmTemplate(),
                new LinearInterpolateTemplate(),
                new FloatingModuloTemplate(this),
                new IsNotANumberTemplate(),
                new FloatClassTemplate(),
                new SmoothStepTemplate(),
                new StepTemplate(),
                new FirstBitHighTemplate(),
                new ClampTemplate(),
                new IntegerVectorComponentTemplate()),

            // Vector idioms: a dot product, and a length over one. Last, because they
            // claim the components of a vector across the values that make it - a
            // dot over a cross product's components takes them from the cross
            // product grouper, which runs later and cannot be asked (see
            // DotProduct2Template). Everything a single value can be is settled
            // before one of these takes it.
            GroupStage("Vector idioms",
                new DotProduct2Template(this),
                new DotProduct3Template(this),
                new DotProduct4Template(this),
                new LengthTemplate()),
        ];
        _nodeGrouper = nodeGrouper;
    }

    /// <summary>
    /// Nothing makes a template reduce anything. Two that undo each other would match
    /// forever - each match builds a fresh node, so the path set cannot see it coming
    /// round again - and the recursion would take the stack with it. A shader that
    /// reaches this is a template pair that does not terminate, not a large shader:
    /// the limit is far above what any real expression needs. Every shader here stays
    /// under fifty, the largest expression included - memoising means a node reduces
    /// once, so the count is bounded by how many nodes there are.
    /// </summary>
    private const int ReductionLimit = 100000;

    private int _reductionsLeft;

    public HlslTreeNode Reduce(HlslTreeNode node)

    {
        // The memo is per call: the graph is rewritten as it reduces, so what a node
        // reduces to only holds for this pass over it.
        _reductionsLeft = ReductionLimit;
        return ReduceDepthFirst(node, HlslTreeNode.NewNodeSet(),
            new Dictionary<HlslTreeNode, HlslTreeNode>(ReferenceEqualityComparer.Instance));
    }

    public bool CanGroupComponents(HlslTreeNode a, HlslTreeNode b, bool allowMatrixColumn)
    {
        return _nodeGrouper.CanGroupComponents(a, b, allowMatrixColumn);
    }

    public bool SharesMatrixColumnOrRow(HlslTreeNode x, HlslTreeNode y)
    {
        if (x is RegisterInputNode r1 && y is RegisterInputNode r2)
        {
            return _nodeGrouper.SharesMatrixColumnOrRow(r1, r2);
        }
        return false;
    }

    // onPath cuts cycles and has to stay a path set to do it. Reducing is what gets
    // remembered instead: without that, a subexpression read in two places is reduced
    // once per path that reaches it, and the work triples with every instruction that
    // builds on the one before.
    /// <summary>
    /// A rewritten node stands for the value the old one did, so it says the same
    /// thing about what that value is. iadd and add are both an AddOperation and
    /// are told apart by this alone, so a template that rebuilt one left the sum of
    /// two bit patterns looking like a sum of floats.
    /// </summary>
    private static void CarryValueType(HlslTreeNode node, HlslTreeNode replacement)
    {
        ValueTypes.Inherit(node, replacement);
        // And which instruction made it: a template rewrites the value, not where
        // it came from, and a hoisted subexpression that has forgotten its
        // instruction cannot be told from one beside it that shares a register.
        if (replacement.SourceInstruction == 0)
        {
            replacement.SourceInstruction = node.SourceInstruction;
            replacement.SourceComponent = node.SourceComponent;
        }
    }

    private HlslTreeNode ReduceDepthFirst(HlslTreeNode node, HashSet<HlslTreeNode> onPath,
        Dictionary<HlslTreeNode, HlslTreeNode> reduced)
    {
        // A phi is opaque: nothing may be folded across a loop backedge.
        if (ConstantMatcher.IsConstant(node) || IsRegister(node) || node is PhiNode)
        {
            return node;
        }
        if (reduced.TryGetValue(node, out HlslTreeNode already))
        {
            return already;
        }
        // Not remembered: a node cut here is only unreduced because of where the walk
        // reached it from.
        if (!onPath.Add(node))
        {
            return node;
        }
        try
        {
            for (int i = 0; i < node.Inputs.Count; i++)
            {
                HlslTreeNode input = node.Inputs[i];
                node.Inputs[i] = ReduceDepthFirst(input, onPath, reduced);
            }
            foreach (Stage stage in _stages)
            {
                foreach (IStageTemplate template in stage.Templates)
                {
                    if (template.TryReduce(node) is not HlslTreeNode replacement)
                    {
                        continue;
                    }
                    CountReduction(stage, template);
                    CarryValueType(node, replacement);
                    Replace(node, replacement);
                    HlslTreeNode result = ReduceDepthFirst(replacement, onPath, reduced);
                    reduced[node] = result;
                    return result;
                }
            }
            reduced[node] = node;
            return node;
        }
        finally
        {
            onPath.Remove(node);
        }
    }

    private static void Replace(HlslTreeNode node, HlslTreeNode with)
    {
        if (node == with)
        {
            return;
        }
        foreach (var input in node.Inputs)
        {
            input.Outputs.Remove(node);
        }
        foreach (var output in node.Outputs)
        {
            for (int i = 0; i < output.Inputs.Count; i++)
            {
                if (output.Inputs[i] == node)
                {
                    output.Inputs[i] = with;
                }
            }
            with.Outputs.Add(output);
        }
    }

    private void CountReduction(Stage stage, IStageTemplate template)
    {
        if (--_reductionsLeft >= 0)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Reducing one expression took more than {ReductionLimit} steps, last by "
            + $"{template.Name} in {stage.Name}. Two templates that undo each other look like this.");
    }

    private static bool IsRegister(HlslTreeNode node)
    {
        return node is RegisterInputNode;
    }
}
