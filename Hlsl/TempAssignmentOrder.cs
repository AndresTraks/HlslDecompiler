using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TempAssignmentOrder : IComparer<HlslTreeNode>
    {
        public int Compare(HlslTreeNode x, HlslTreeNode y)
        {
            if (IsInputOf(x, y))
            {
                return -1;
            }
            if (IsInputOf(y, x))
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
            else if (input is VirtualGroupNode inputGroup)
            {
                if (inputGroup.Inputs.Any(i => IsInputOf(i, node)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
