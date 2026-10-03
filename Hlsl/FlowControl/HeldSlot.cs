using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// One of the places a statement holds a value outside its input and output maps,
/// declared once: how to read it, how to write it, and whether the statement names
/// what is in it. Every question <see cref="IStatement"/> answers about held values
/// is derived from these, so a slot cannot be reported by one answer and missed by
/// another - which is the whole reason they exist rather than three hand-written
/// members per statement.
/// </summary>
public class HeldSlot
{
    private readonly Func<IEnumerable<HlslTreeNode>> _read;
    private readonly Action<HlslTreeNode, HlslTreeNode> _replace;

    private HeldSlot(
        bool isNamed,
        Func<IEnumerable<HlslTreeNode>> read,
        Action<HlslTreeNode, HlslTreeNode> replace)
    {
        IsNamed = isNamed;
        _read = read;
        _replace = replace;
    }

    /// <summary>Whether the statement prints what is here by whatever name it ends
    /// up under, rather than writing it out again. See <see
    /// cref="IStatement.NamedHeldNodes"/> for what turns on it.</summary>
    public bool IsNamed { get; }

    /// <summary>What is in the slot now, with the empty places left out: a store
    /// masked to two components holds a null for each of the others.</summary>
    public IEnumerable<HlslTreeNode> Nodes => _read().Where(node => node != null);

    public void Replace(HlslTreeNode node, HlslTreeNode replacement)
    {
        _replace?.Invoke(node, replacement);
    }

    /// <summary>A named reference the statement can be made to read elsewhere.</summary>
    public static HeldSlot Named(Func<HlslTreeNode> read, Action<HlslTreeNode> write)
    {
        return new HeldSlot(
            true,
            () => [read()],
            (node, replacement) =>
            {
                if (read() == node)
                {
                    write(replacement);
                }
            });
    }

    /// <summary>A named run of references - the values of a store, the components of
    /// a coordinate - rewritten where they sit, since the array is the slot.</summary>
    public static HeldSlot Named(Func<HlslTreeNode[]> read)
    {
        return new HeldSlot(
            true,
            () => read() ?? [],
            (node, replacement) =>
            {
                HlslTreeNode[] nodes = read();
                for (int i = 0; i < (nodes?.Length ?? 0); i++)
                {
                    if (nodes[i] == node)
                    {
                        nodes[i] = replacement;
                    }
                }
            });
    }

    /// <summary>A value the statement writes out again wherever it appears, so there
    /// is no name to redirect and no setter to need. Held all the same: the walks
    /// have to reach it.</summary>
    public static HeldSlot WrittenAgain(Func<HlslTreeNode> read)
    {
        return new HeldSlot(false, () => [read()], null);
    }

    /// <summary>The same, for a statement that writes out several - a switch and its
    /// case labels, an if and its comparisons.</summary>
    public static HeldSlot WrittenAgain(Func<IEnumerable<HlslTreeNode>> read)
    {
        return new HeldSlot(false, () => read() ?? [], null);
    }
}
