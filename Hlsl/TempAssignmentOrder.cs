using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class TempAssignmentOrder : IComparer<HlslTreeNode>, IComparer<HlslTreeNode[]>
{
    public int Compare(HlslTreeNode[] x, HlslTreeNode[] y)
    {
        if (x.Any(i => y.Any(i2 => IsInputOf(i, i2))))
        {
            return -1;
        }
        if (y.Any(i => x.Any(i2 => IsInputOf(i, i2))))
        {
            return 1;
        }
        return 0;
    }

    public int Compare(HlslTreeNode x, HlslTreeNode y)
    {
        if (x.IsInputOf(y))
        {
            return -1;
        }
        if (y.IsInputOf(x))
        {
            return 1;
        }
        return 0;
    }

    private static bool IsInputOf(HlslTreeNode input, HlslTreeNode node)
    {
        if (input is TempAssignmentNode tempAssignment)
        {
            if (tempAssignment.IsInputOf(node) || tempAssignment.TempVariable.IsInputOf(node))
            {
                return true;
            }
        }
        return false;
    }
}
