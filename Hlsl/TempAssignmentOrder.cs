using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class TempAssignmentOrder : IComparer<HlslTreeNode>
    {
        public int Compare(HlslTreeNode x, HlslTreeNode y)
        {
            if (x is GroupNode xGroup)
            {
                if (xGroup.Inputs.Any(i => i.IsInputOf(y)))
                {
                    return -1;
                }
            }
            else if (x.IsInputOf(y))
            {
                return -1;
            }
            if (y is GroupNode yGroup)
            {
                if (yGroup.Inputs.Any(i => i.IsInputOf(x)))
                {
                    return 1;
                }
            }
            else if (y.IsInputOf(x))
            {
                return 1;
            }
            return 0;
        }
    }
}
