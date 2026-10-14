using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.TemplateMatch;
using NUnit.Framework;

namespace HlslDecompiler.Tests;

/// <summary>
/// <see cref="CompareCompareTemplate"/>: `(v >= 0 ? 1 : 0) != 0` is `v >= 0`.
/// fxc never emits this shape, so no fixture reaches it.
/// </summary>
[TestFixture]
public class CompareCompareTemplateTests
{
    // cmp writes src1 when src0 >= 0, else src2.
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

    // Usually CompareConstantTemplate swaps a left constant first.
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

    [Test]
    public void DoesNotMatchACmpWithTwoEqualBranches()
    {
        var node = new ComparisonNode(
            Cmp(new ConstantNode(7), 1, 1), new ConstantNode(1), IfComparison.EQ);

        Assert.That(new CompareCompareTemplate().Match(node), Is.False);
    }

    [Test]
    public void DoesNotMatchAConstantNeitherBranchProduces()
    {
        var node = new ComparisonNode(
            Cmp(new ConstantNode(7), 1, -1), new ConstantNode(0), IfComparison.EQ);

        Assert.That(new CompareCompareTemplate().Match(node), Is.False);
    }
}
