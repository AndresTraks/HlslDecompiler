using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public abstract class Operation : HlslTreeNode
    {
        public abstract string Mnemonic { get; }

        public override string ToString()
        {
            string parameters = string.Join(" ,", Children.Select(c => c.ToString()));
            return $"{Mnemonic}({parameters})";
        }
    }
}
