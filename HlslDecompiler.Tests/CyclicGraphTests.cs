using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.FlowControl;
using NUnit.Framework;
using System.Collections.Generic;

namespace HlslDecompiler.Tests;

/// <summary>
/// A loop-header phi is cyclic, and every traversal must terminate on it: a
/// StackOverflowException kills the whole test process.
/// <see cref="PhiNode.SetBackedgeValue"/> is the only way to create a cycle.
/// </summary>
[TestFixture]
public class CyclicGraphTests
{
    // t = phi(0, t + 1)
    private static PhiNode BuildLoopCarriedCounter(out HlslTreeNode backedgeValue)
    {
        var phi = new PhiNode(new ConstantNode(0));
        backedgeValue = new AddOperation(phi, new ConstantNode(1));
        phi.SetBackedgeValue(backedgeValue);
        return phi;
    }

    [Test]
    public void LoopHeaderPhiIsCyclic()
    {
        PhiNode phi = BuildLoopCarriedCounter(out HlslTreeNode backedgeValue);

        Assert.Multiple(() =>
        {
            Assert.That(phi.IsLoopHeader, Is.True);
            Assert.That(phi.BackedgeValue, Is.SameAs(backedgeValue));
            Assert.That(backedgeValue.Inputs, Does.Contain(phi));
            Assert.That(phi.Inputs, Does.Contain(backedgeValue));
        });
    }

    [Test]
    public void IsInputOfTerminates()
    {
        PhiNode phi = BuildLoopCarriedCounter(out HlslTreeNode backedgeValue);

        Assert.Multiple(() =>
        {
            // A phi is a leaf to expression traversals.
            Assert.That(phi.IsInputOf(backedgeValue), Is.True);
            Assert.That(backedgeValue.IsInputOf(phi), Is.False);
        });
    }

    [Test]
    public void NodeVisitorTerminates()
    {
        PhiNode phi = BuildLoopCarriedCounter(out HlslTreeNode backedgeValue);

        var visited = new List<HlslTreeNode>();
        new NodeVisitor([backedgeValue]).Visit(visited.Add);

        Assert.That(visited, Does.Contain(phi));
    }

    [Test]
    public void ToStringTerminates()
    {
        PhiNode phi = BuildLoopCarriedCounter(out _);

        Assert.That(phi.ToString(), Does.Contain("loop"));
    }
}
