using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.FlowControl;
using NUnit.Framework;
using System.Collections.Generic;

namespace HlslDecompiler.Tests;

/// <summary>
/// A loop-header phi is cyclic by construction: the body reads the phi, and the
/// phi's backedge operand is derived from the body. Every traversal must terminate
/// on that, because the failure mode is StackOverflowException - which .NET cannot
/// catch and which takes the whole test process with it rather than failing a test.
///
/// <see cref="PhiNode.SetBackedgeValue"/> is the only path that may create a cycle;
/// the ordinary <c>AddInput</c> still asserts the graph stays acyclic.
/// </summary>
[TestFixture]
public class CyclicGraphTests
{
    /// <summary>Builds `t = phi(0, t + 1)`, the shape a counter in a loop produces.</summary>
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
            // The cycle: the backedge value reads the phi it feeds.
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
            // A phi is a leaf to expression traversals, so walking down from the
            // backedge value stops at the phi rather than going round again.
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
