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
                new CompareConstantTemplate(),
                new CompareNegativeWithZeroTemplate(),
                new CompareAbsoluteWithZeroTemplate(),
                new AddMaskTemplate(),
                new CompareSelectedConstantTemplate(),
                new ConvertSelectedConstantTemplate(),
                new ComparePositiveAndNegativeTemplate(),
                new CompareCompareTemplate()),

            // Each matches one value; none claims another value's components.
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
