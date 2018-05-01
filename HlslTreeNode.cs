using System.Collections.Generic;
using System.Globalization;

namespace HlslDecompiler
{
    public class HlslTreeNode
    {
        public IList<HlslTreeNode> Children { get; } = new List<HlslTreeNode>();
        public IList<HlslTreeNode> Parents { get; } = new List<HlslTreeNode>();

        public void AddChild(HlslTreeNode node)
        {
            Children.Add(node);
            node.Parents.Add(this);
        }

        public virtual HlslTreeNode Reduce()
        {
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i] = Children[i].Reduce();
            }
            return this;
        }

        public void Replace(HlslTreeNode with)
        {
            foreach (var child in Children)
            {
                child.Parents.Remove(this);
            }
            foreach (var parent in Parents)
            {
                for (int i = 0; i < parent.Children.Count; i++)
                {
                    if (parent.Children[i] == this)
                    {
                        parent.Children[i] = with;
                    }
                }
                with.Parents.Add(parent);
            }
        }
    }

    public class HlslConstant : HlslTreeNode
    {
        public float Value { get; }

        public HlslConstant(float value)
        {
            Value = value;
        }

        public override bool Equals(object obj)
        {
            return obj is HlslConstant && this == (HlslConstant)obj;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        public static bool operator ==(HlslConstant x, HlslConstant y)
        {
            if (ReferenceEquals(x, null)) return ReferenceEquals(y, null);
            if (ReferenceEquals(y, null)) return false;
            return x.Value == y.Value;
        }
        public static bool operator !=(HlslConstant x, HlslConstant y)
        {
            return !(x == y);
        }

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }

    public class HlslShaderInput : HlslTreeNode
    {
        public RegisterKey InputDecl { get; set; }
        public int ComponentIndex { get; set; }

        public override string ToString()
        {
            return $"{InputDecl} ({ComponentIndex})";
        }
    }
}
