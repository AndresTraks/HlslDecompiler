using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class LoopStatement : IStatement
    {
        public uint RepeatCount { get; }
        public IList<IStatement> Body { get; } = [];
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public bool IsParsed { get; set; } = false;

        public LoopStatement(uint repeatCount, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            RepeatCount = repeatCount;
            Inputs = inputs.ToDictionary();
            Outputs = inputs.ToDictionary();
        }
    }
}
