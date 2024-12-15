using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class StatementSequence : IStatement
    {
        public Dictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; set; }

        public Dictionary<RegisterKey, HlslTreeNode> GroupAssignments()
        {
            return GroupComponents(Outputs.Where(IsAssignment));
        }

        public Dictionary<RegisterKey, HlslTreeNode> GroupOutputs()
        {
            return GroupComponents(Outputs.Where(IsOutput));
        }

        private static Dictionary<RegisterKey, HlslTreeNode> GroupComponents(IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> outputsByComponent)
        {
            return outputsByComponent
                .OrderBy(o => o.Key.ComponentIndex)
                .GroupBy(o => o.Key.RegisterKey)
                .ToDictionary(
                    o => o.Key,
                    o => (HlslTreeNode)new GroupNode(o.Select(o => o.Value).ToArray()));
        }

        private static bool IsAssignment(KeyValuePair<RegisterComponentKey, HlslTreeNode> operation)
        {
            if (operation.Key.RegisterKey is D3D9RegisterKey key9)
            {
                return key9.Type == RegisterType.Temp;
            }
            else if (operation.Key.RegisterKey is D3D10RegisterKey key10)
            {
                return key10.OperandType == OperandType.Temp;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static bool IsOutput(KeyValuePair<RegisterComponentKey, HlslTreeNode> operation)
        {
            if (operation.Key.RegisterKey is D3D9RegisterKey key9)
            {
                RegisterType type = key9.Type;
                return type == RegisterType.Output || type == RegisterType.ColorOut || type == RegisterType.DepthOut;
            }
            else if (operation.Key.RegisterKey is D3D10RegisterKey key10)
            {
                return key10.OperandType == OperandType.Output;
            }
            else
            {
                throw new NotImplementedException();
            }
        }
    }
}
