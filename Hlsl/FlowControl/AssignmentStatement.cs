using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class AssignmentStatement : IStatement
    {
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }

        public AssignmentStatement(IDictionary<RegisterComponentKey, HlslTreeNode> inputs)
        {
            Inputs = inputs.ToDictionary();
            Outputs = inputs.ToDictionary();
        }

        public override string ToString()
        {
            string keys = "";
            string values = "";
            foreach (var output in Outputs)
            {
                if (keys.Length != 0) keys += ", ";
                keys += output.Key.ToString();
                if (values.Length != 0) values += ", ";
                values += output.Value.ToString();
            }
            return keys + " = " + values;
        }
    }
}
