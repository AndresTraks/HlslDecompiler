using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Tests;

[TestFixture]
public class TempAssignmentOrderTests
{
    private static TempVariableNode Variable(int index)
    {
        return new TempVariableNode { DeclarationIndex = index, ComponentIndex = 0, VariableSize = 1 };
    }

    [TestCase(4)]
    [TestCase(8)]
    [TestCase(20)]
    [TestCase(40)]
    public void SortRespectsOneDependencyAmongManyUnrelated(int count)
    {
        // One real dependency: the last assignment reads the first variable. Every
        // other pair is unrelated, so the comparator calls them equal - which makes
        // "equal" non-transitive, and a comparison sort may place them anywhere.
        var shared = Variable(0);
        var producer = new TempAssignmentNode(shared, new AddOperation(new ConstantNode(1), new ConstantNode(2)));
        var consumer = new TempAssignmentNode(Variable(count - 1), new MultiplyOperation(shared, new ConstantNode(5)));

        var all = new List<TempAssignmentNode> { consumer };
        for (int i = 1; i < count - 1; i++)
        {
            all.Add(new TempAssignmentNode(Variable(i), new AddOperation(new ConstantNode(i), new ConstantNode(i))));
        }
        all.Add(producer);

        var list = TempAssignmentOrder.Sort(all.Select(n => new HlslTreeNode[] { n }));

        int producerAt = list.FindIndex(g => ReferenceEquals(g[0], producer));
        int consumerAt = list.FindIndex(g => ReferenceEquals(g[0], consumer));
        TestContext.Out.WriteLine($"count {count}: producer at {producerAt}, consumer at {consumerAt}");
        Assert.That(producerAt, Is.LessThan(consumerAt),
            $"count {count}: the assignment defining the variable must be written first");
    }
}
