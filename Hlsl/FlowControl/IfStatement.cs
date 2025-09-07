using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class IfStatement : IStatement
    {
        public HlslTreeNode[] Comparison { get; }
        public IList<IStatement> TrueBody { get; set; } = [];
        public IList<IStatement> FalseBody { get; set; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public bool IsTrueParsed { get; set; } = false;
        public bool IsParsed { get; set; } = false;

        public IfStatement(HlslTreeNode[] comparison, IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Comparison = comparison;
            Inputs = inputs.ToDictionary();
            Outputs = inputs.ToDictionary();
        }

        public override string ToString()
        {
            return "if (" + string.Join(", ", Comparison.Select(c => c.ToString())) + ")";
        }
    }
}
