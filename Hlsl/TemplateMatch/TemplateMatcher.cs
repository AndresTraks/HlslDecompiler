using HlslDecompiler.Hlsl.FlowControl;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.TemplateMatch;

public class TemplateMatcher
{
    /// <summary>
    /// Templates in the order tried at a node. The first match rewrites it and the
    /// result is reduced again from the first stage.
    /// </summary>
    private readonly IReadOnlyList<Stage> _stages;

    private sealed record Stage(string Name, IReadOnlyList<IStageTemplate> Templates);

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
    /// Resolves a variable (a leaf to templates) to the loop-invariant expression
    /// behind it. Null until the writer builds it.
    /// </summary>
    public TempResolver TempResolver { get; set; }

    /// <summary>
    /// HlslAst.DoubleValues, kept up to date by rewrites.
    /// </summary>
    public ISet<HlslTreeNode> DoubleValues { get; set; }

    /// <summary>
    /// The lanes of each multiply instruction, by instruction position. Null
    /// until the writer builds it.
    /// </summary>
    public IReadOnlyDictionary<int, HashSet<HlslTreeNode>> InstructionProducts { get; set; }

    /// <summary>
    /// The statements' own values, before the finalizer: a lane among them is
    /// read whole.
    /// </summary>
    public HashSet<HlslTreeNode> StatementValues { get; set; }

    /// <summary>
    /// Whether the sum chain over these two lanes of one multiply leaves out a lane
    /// that needs the multiply anyway (`p + p.yzwx`, or `p.w` read alone), which
    /// then serves the sum too, where dots would multiply again. A chain over
    /// every lane is a dot on its own, and a lane read only by its own sum (two
    /// matrix rows in one multiply) is a dot of its own.
    /// </summary>
    public bool LeavesInstructionLanesOut(HlslTreeNode sum, HlslTreeNode product1, HlslTreeNode product2)
    {
        if (InstructionProducts == null || product1.SourceInstruction == 0
            || !HlslTreeNode.IsSameInstruction(product1, product2)
            || !InstructionProducts.TryGetValue(product1.SourceInstruction, out HashSet<HlslTreeNode> lanes))
        {
            return false;
        }
        // Up through sums and the mads adding to it, which multiply a lane again.
        HlslTreeNode top = sum;
        while (top.Outputs.Count == 1 && (top.Outputs[0] is AddOperation
            || top.Outputs[0] is MultiplyAddOperation mad && ReferenceEquals(mad.Addend, top)))
        {
            top = top.Outputs[0];
        }
        var leaves = new List<(HlslTreeNode, HlslTreeNode)>();
        var pending = new Stack<HlslTreeNode>([top]);
        while (pending.Count != 0)
        {
            switch (pending.Pop())
            {
                case AddOperation add:
                    pending.Push(add.Addend1);
                    pending.Push(add.Addend2);
                    break;
                case MultiplyAddOperation mad:
                    leaves.Add((mad.Factor1, mad.Factor2));
                    pending.Push(mad.Addend);
                    break;
                case MultiplyOperation product:
                    leaves.Add((product.Factor1, product.Factor2));
                    break;
            }
        }
        return lanes.Any(lane => !leaves.Any(leaf => IsSameProduct(leaf, lane.Inputs[0], lane.Inputs[1]))
            && IsReadWhole(lane));
    }

    // Read other than by a single sum: more than once, by another operation, or as a statement's value.
    private bool IsReadWhole(HlslTreeNode lane)
    {
        return lane.Outputs.Count != 1
            || lane.Outputs[0] is not (AddOperation or MultiplyAddOperation)
            || StatementValues?.Contains(lane) == true;
    }

    private static bool IsSameProduct((HlslTreeNode, HlslTreeNode) leaf, HlslTreeNode factor1, HlslTreeNode factor2)
    {
        (HlslTreeNode a, HlslTreeNode b) = leaf;
        return NodeGrouper.AreNodesEquivalent(a, factor1) && NodeGrouper.AreNodesEquivalent(b, factor2)
            || NodeGrouper.AreNodesEquivalent(a, factor2) && NodeGrouper.AreNodesEquivalent(b, factor1);
    }

    public TemplateMatcher(NodeGrouper nodeGrouper)
    {
        _stages =
        [
            // First, so later stages see one canonical spelling of each value.
            NodeStage("Arithmetic identities",
                new AddConstantsTemplate(),
                new BitwiseConstantsTemplate(),
                new ModuloConstantsTemplate(),
                new AddNegateTemplate(),
                new AddNegativeTemplate(),
                new AddSelfTemplate(),
                new AddZeroTemplate(),
                new MoveTemplate(),
                new DoubleRejoinTemplate(),
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
            // NegateSubtractTemplate (`-(a - b)` as `b - a`) is not used; why is not recorded.

            // Before the intrinsics, which match sign, step etc. by these comparisons.
            NodeStage("Comparisons and selects",
                new SetComparisonConstantTemplate(),
                new SetComparisonSelectedTemplate(),
                new SetComparisonTestedTemplate(),
                new CompareConstantTemplate(),
                new CompareNegativeWithZeroTemplate(),
                new CompareAbsoluteWithZeroTemplate(),
                new AddMaskTemplate(),
                new CompareSelectedConstantTemplate(),
                new ConvertSelectedConstantTemplate(),
                new ComparePositiveAndNegativeTemplate(),
                new CompareCompareTemplate(),
                new ComparisonSelectedConstantTemplate()),

            // Each matches one value; none claims another value's components.
            NodeStage("Intrinsics and idioms",
                new MaxOfPositiveAndNegativeTemplate(),
                new TrigonometricRangeReductionTemplate(),
                new SignedDivideTemplate(this),
                new SignTemplate(),
                new FloorTemplate(),
                new TruncateTemplate(),
                new SignedFloorTruncateTemplate(),
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

            // Last: these claim components across values (see DotProduct2Template).
            GroupStage("Vector idioms",
                new DotProduct2Template(this),
                new DotProduct3Template(this),
                new DotProduct4Template(this),
                new LengthTemplate()),
        ];
        _nodeGrouper = nodeGrouper;
    }

    /// <summary>
    /// Guards against two templates that undo each other, which the path set cannot
    /// detect since each match builds a fresh node. Far above any real expression.
    /// </summary>
    private const int ReductionLimit = 100000;

    private int _reductionsLeft;

    public HlslTreeNode Reduce(HlslTreeNode node)

    {
        // The memo is per call: the graph is rewritten as it reduces.
        _reductionsLeft = ReductionLimit;
        return ReduceDepthFirst(node, HlslTreeNode.NewNodeSet(),
            new Dictionary<HlslTreeNode, HlslTreeNode>(ReferenceEqualityComparer.Instance));
    }

    public bool CanGroupComponents(HlslTreeNode a, HlslTreeNode b, bool allowMatrixColumn)
    {
        return _nodeGrouper.CanGroupComponents(a, b, allowMatrixColumn);
    }

    public bool MayBeMatrixRows(params HlslTreeNode[] components)
    {
        return _nodeGrouper.MayBeMatrixRows(components);
    }

    /// <summary>
    /// Dots that no other grouper claims: not matrix rows, and the cross and
    /// normalize groupers take products, not dots.
    /// </summary>
    public bool AreFreeDots(params HlslTreeNode[] components)
    {
        return _nodeGrouper.AreFreeDots(components);
    }

    public bool SharesMatrixColumnOrRow(HlslTreeNode x, HlslTreeNode y)
    {
        if (x is RegisterInputNode r1 && y is RegisterInputNode r2)
        {
            return _nodeGrouper.SharesMatrixColumnOrRow(r1, r2);
        }
        return false;
    }

    /// <summary>
    /// Carries the value type to the replacement: iadd and add are both an
    /// AddOperation, told apart only by this.
    /// </summary>
    private static void CarryValueType(HlslTreeNode node, HlslTreeNode replacement)
    {
        ValueTypes.Inherit(node, replacement);
        // And the source instruction, which tells apart values sharing a register.
        if (replacement.SourceInstruction == 0)
        {
            replacement.SourceInstruction = node.SourceInstruction;
            replacement.SourceComponent = node.SourceComponent;
            replacement.MinPrecision = node.MinPrecision;
        }
    }

    // onPath cuts cycles; the reduced memo keeps shared subexpressions from being
    // reduced once per path.
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
        // Not memoised: it is unreduced only because of the path taken.
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
                    // DoubleValues records nodes, so the replacement must be added.
                    if (DoubleValues != null && DoubleValues.Contains(node))
                    {
                        DoubleValues.Add(replacement);
                    }
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
