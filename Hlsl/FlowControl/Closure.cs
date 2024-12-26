using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class Closure
    {
        public Dictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public Closure(Dictionary<RegisterComponentKey, HlslTreeNode> outputs)
        {
            Outputs = outputs;
        }
    }
}
