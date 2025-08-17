using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public interface IStatement
    {
        public IDictionary<RegisterComponentKey, HlslTreeNode> Inputs { get; }
        public IDictionary<RegisterComponentKey, HlslTreeNode> Outputs { get; }
    }
}
