using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl.TemplateMatch;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class HlslAst
    {
        public List<StatementSequence> Statements { get; private set; }
        public RegisterState RegisterState { get; private set; }

        public HlslAst(List<StatementSequence> statements, RegisterState registerState)
        {
            Statements = statements;
            RegisterState = registerState;
        }

        public List<Dictionary<RegisterKey, HlslTreeNode>> ReduceTree(NodeGrouper nodeGrouper)
        {
            var templateMatcher = new TemplateMatcher(nodeGrouper);
            return Statements
                .Select(GroupOutputs)
                .Select(outputs => outputs.ToDictionary(r => r.Key, r => templateMatcher.Reduce(r.Value)))
                .ToList();
        }

        public static Dictionary<RegisterKey, HlslTreeNode> GroupOutputs(StatementSequence statements)
        {
            IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent =
                statements.Outputs.Where(o =>
            {
                if (o.Value is ClipOperation)
                {
                    return true; 
                }

                if (o.Key.RegisterKey is D3D9RegisterKey key9)
                {
                    RegisterType type = key9.Type;
                    return type == RegisterType.Output || type == RegisterType.ColorOut || type == RegisterType.DepthOut;
                }
                else if (o.Key.RegisterKey is D3D10RegisterKey key10)
                {
                    return key10.OperandType == OperandType.Output;
                }
                else
                {
                    throw new NotImplementedException();
                }
            });
            var outputsByRegister = outputsByComponent
                .OrderBy(o => o.Key.ComponentIndex)
                .GroupBy(o => o.Key.RegisterKey)
                .ToDictionary(
                    o => o.Key,
                    o => (HlslTreeNode)new GroupNode(o.Select(o => o.Value).ToArray()));
            return outputsByRegister;
        }
    }
}
