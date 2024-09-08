using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl
{
    public class StatementSequence
    {
        public Dictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; set; }
    }
}
