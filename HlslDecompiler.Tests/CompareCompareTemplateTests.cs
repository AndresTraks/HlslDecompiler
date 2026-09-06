using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.TemplateMatch;
using NUnit.Framework;

namespace HlslDecompiler.Tests;

/// <summary>
/// <see cref="CompareCompareTemplate"/> rewrites a branch on a cmp result into a
/// branch on what the cmp tested: `(v >= 0 ? 1 : 0) != 0` is just `v >= 0`.
///
/// None of the shader fixtures reach it. fxc branches on the original value rather
/// than on a cmp result, so the shape only turns up in bytecode it did not produce,
/// which leaves these the only tests it has.
/// </summary>
[TestFixture]
public class CompareCompareTemplateTests
{
    /// <summary>
    /// `cmp dst, src0, src1, src2` writes src1 when src0 >= 0 and src2 when it is
    /// negative, which is the order <see cref="Hlsl.NodeCompiler"/> emits.
    /// </summary>
    private static CompareOperation Cmp(HlslTreeNode value, float whenGreaterEqual, float whenLess)
    {
        return new CompareOperation(value,
            new ConstantNode(whenGreaterEqual),
            new ConstantNode(whenLess));
    }

    [TestCase(IfComparison.EQ, 1f, IfComparison.GE)]
    [TestCase(IfComparison.EQ, 0f, IfComparison.LT)]
    [TestCase(IfComparison.NE, 0f, IfComparison.GE)]
    [TestCase(IfComparison.NE, 1f, IfComparison.LT)]
    public void ReducesToTheComparisonTheCompareMade(
        IfComparison comparison, float constant, IfComparison expected)
    {
        var value = new ConstantNode(7);
        var node = new ComparisonNode(Cmp(value, 1, 0), new ConstantNode(constant), comparison);
        var template = new CompareCompareTemplate();

        Assert.That(template.Match(node), Is.True);

        var reduced = template.Reduce(node) as ComparisonNode;
        Assert.Multiple(() =>
        {
            Assert.That(reduced.Comparison, Is.EqualTo(expected));
            Assert.That(reduced.Left, Is.SameAs(value));
            Assert.That(ConstantMatcher.IsZero(reduced.Right), Is.True);
        });
    }

    /// <summary>
    /// Equality reads the same either way round, so the mirrored form reduces to the
    /// same comparison. In a full reduction it rarely arrives here - a constant on the
    /// left is what CompareConstantTemplate swaps, and it is registered earlier.
    /// </summary>
    [TestCase(IfComparison.EQ, 1f, IfComparison.GE)]
    [TestCase(IfComparison.EQ, 0f, IfComparison.LT)]
    [TestCase(IfComparison.NE, 0f, IfComparison.GE)]
    [TestCase(IfComparison.NE, 1f, IfComparison.LT)]
    public void ReducesTheSameWithTheCompareOnTheRight(
        IfComparison comparison, float constant, IfComparison expected)
    {
        var value = new ConstantNode(7);
        var node = new ComparisonNode(new ConstantNode(constant), Cmp(value, 1, 0), comparison);
        var template = new CompareCompareTemplate();

        Assert.That(template.Match(node), Is.True);

        var reduced = template.Reduce(node) as ComparisonNode;
        Assert.Multiple(() =>
        {
            Assert.That(reduced.Comparison, Is.EqualTo(expected));
            Assert.That(reduced.Left, Is.SameAs(value));
            Assert.That(ConstantMatcher.IsZero(reduced.Right), Is.True);
        });
    }

    /// <summary>
    /// Both branches carrying the same value makes the comparison a tautology, which
    /// says nothing about the value the cmp tested.
    /// </summary>
    [Test]
    public void DoesNotMatchACmpWithTwoEqualBranches()
    {
        var node = new ComparisonNode(
            Cmp(new ConstantNode(7), 1, 1), new ConstantNode(1), IfComparison.EQ);

        Assert.That(new CompareCompareTemplate().Match(node), Is.False);
    }

    /// <summary>A cmp whose result does not decide the comparison is left alone.</summary>
    [Test]
    public void DoesNotMatchAConstantNeitherBranchProduces()
    {
        var node = new ComparisonNode(
            Cmp(new ConstantNode(7), 1, -1), new ConstantNode(0), IfComparison.EQ);

        Assert.That(new CompareCompareTemplate().Match(node), Is.False);
    }
}
