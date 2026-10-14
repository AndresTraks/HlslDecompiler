using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// A place a statement holds a value outside its input and output maps: how to read
/// and write it, and whether it is named. Every held-value query on <see
/// cref="IStatement"/> derives from these, so none can miss a slot.
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

    /// <summary>Printed by name rather than written out again; see <see
    /// cref="IStatement.NamedHeldNodes"/>.</summary>
    public bool IsNamed { get; }

    /// <summary>Without nulls: a masked store holds null for unwritten components.</summary>
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

    /// <summary>A named array, rewritten in place since the array is the slot.</summary>
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

    /// <summary>A value written out again where it appears, so it needs no setter.
    /// Still held so that the walks reach it.</summary>
    public static HeldSlot WrittenAgain(Func<HlslTreeNode> read)
    {
        return new HeldSlot(false, () => [read()], null);
    }

    public static HeldSlot WrittenAgain(Func<IEnumerable<HlslTreeNode>> read)
    {
        return new HeldSlot(false, () => read() ?? [], null);
    }
}
