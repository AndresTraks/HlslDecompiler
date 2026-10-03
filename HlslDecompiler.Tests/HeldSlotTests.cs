using HlslDecompiler.Hlsl;
using HlslDecompiler.Hlsl.FlowControl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HlslDecompiler.Tests;

/// <summary>
/// A statement holds values outside its input and output maps - the address and
/// values of a store, the comparison a break tests - and those references are not
/// edges in the value graph, so nothing that walks or rewrites the graph reaches
/// them. Every pass that needs them asks the statement, through the slots it
/// declares in IStatement.HeldSlots.
///
/// The one thing that can still go wrong is a held field with no slot. It compiles,
/// every walk silently skips it, and what it holds is judged dead or left pointing
/// at a node that has been replaced - which is the shape of the bug cbadf26 fixed,
/// when the same knowledge was a switch over the statement types in seven places.
/// So the slots are checked against the fields rather than trusted: add a node to a
/// statement and this fails until it is either slotted or excluded here, with the
/// reason written down.
/// </summary>
[TestFixture]
public class HeldSlotTests
{
    /// <summary>
    /// The node-typed members that are deliberately not held values, and why. Each
    /// is a reference to something the statement does not compile as an expression.
    /// </summary>
    private static readonly Dictionary<string, string> NotHeldValues = new()
    {
        ["AtomicStatement.Destination"] = "the resource operated on, named from its register",
        ["AtomicStatement.ElementByteOffset"] = "read as the number it is, to name the member",
        ["AtomicStatement.Original"] = "the variable the old value goes into, which is an output",
        ["BufferAppendStatement.Destination"] = "the resource appended to, named from its register",
        ["StoreStructuredStatement.Destination"] = "the resource written, named from its register",
        ["StoreTypedStatement.Destination"] = "the view written, named from its register",
    };

    private static IEnumerable<Type> StatementTypes()
    {
        return typeof(IStatement).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IStatement).IsAssignableFrom(type))
            .OrderBy(type => type.Name);
    }

    // A name per question, so that a failure says which of the two it was.
    private static IEnumerable<TestCaseData> Statements()
    {
        return StatementTypes().Select(type =>
            new TestCaseData(type).SetName($"HeldSlots({type.Name})"));
    }

    private static IEnumerable<TestCaseData> StatementsToRedirect()
    {
        return StatementTypes().Select(type =>
            new TestCaseData(type).SetName($"HeldSlotRedirect({type.Name})"));
    }

    [TestCaseSource(nameof(Statements))]
    public void EveryNodeAStatementHoldsReachesASlot(Type type)
    {
        foreach (PropertyInfo property in NodeProperties(type))
        {
            IStatement statement = Empty(type);
            HlslTreeNode sentinel = Sentinel(property);
            Set(statement, property, sentinel);

            bool reached = statement.HeldNodes.Contains(sentinel);
            string name = $"{type.Name}.{property.Name}";
            if (NotHeldValues.TryGetValue(name, out string reason))
            {
                Assert.That(reached, Is.False,
                    $"{name} is excluded as {reason}, but a slot reports it anyway.");
                continue;
            }
            Assert.That(reached, Is.True,
                $"{name} holds a node that no slot in {type.Name}.HeldSlots reports, so "
                + "every walk over the statements skips it. Give it a slot, or add it to "
                + "NotHeldValues with the reason it is not a value.");
        }
    }

    /// <summary>
    /// And that a named slot can actually be redirected. A slot declares how to read
    /// and how to write one place; a setter that writes the wrong one would report
    /// the value and then fail to move it, which is the same bug seen from the other
    /// side. The slots that are not named are checked to hold still, because that is
    /// what being written out again at the branch means.
    /// </summary>
    [TestCaseSource(nameof(StatementsToRedirect))]
    public void ANamedSlotIsRedirectedAndOthersHoldStill(Type type)
    {
        foreach (PropertyInfo property in NodeProperties(type))
        {
            IStatement statement = Empty(type);
            HlslTreeNode sentinel = Sentinel(property);
            Set(statement, property, sentinel);
            if (!statement.HeldNodes.Contains(sentinel))
            {
                continue;
            }

            bool named = statement.NamedHeldNodes.Contains(sentinel);
            HlslTreeNode replacement = Sentinel(property);
            statement.ReplaceHeldNode(sentinel, replacement);

            string name = $"{type.Name}.{property.Name}";
            if (named)
            {
                Assert.That(statement.HeldNodes.Contains(replacement), Is.True,
                    $"{name} is a named slot, so ReplaceHeldNode has to redirect it.");
                Assert.That(statement.HeldNodes.Contains(sentinel), Is.False,
                    $"{name} still holds the node it was told to stop holding.");
            }
            else
            {
                Assert.That(statement.HeldNodes.Contains(sentinel), Is.True,
                    $"{name} is written out again rather than named, so nothing should "
                    + "redirect it - see IStatement.NamedHeldNodes for what it costs.");
            }
        }
    }

    /// <summary>
    /// A switch's labels hang off its cases rather than off the switch, so they are
    /// reached through the one slot that projects them and are checked here instead.
    /// </summary>
    [Test]
    public void ASwitchHoldsItsCaseLabels()
    {
        var label = new ConstantNode(3);
        var switchStatement = Empty(typeof(SwitchStatement));
        SetField(switchStatement, typeof(SwitchStatement).GetProperty(nameof(SwitchStatement.Cases)),
            new List<SwitchCase> { new(label) });

        Assert.That(switchStatement.HeldNodes.Contains(label), Is.True);
        Assert.That(switchStatement.NamedHeldNodes.Contains(label), Is.False);
    }

    /// <summary>
    /// The members that hold a node or a run of them. A dictionary of register keys
    /// is a map and not a slot, and anything else is not a node at all.
    ///
    /// Auto-properties only, since a sentinel goes in through the backing field. A
    /// statement that held a node behind a hand-written property would go unchecked;
    /// none does, and a plain property is the shape the parser builds anyway.
    /// </summary>
    private static IEnumerable<PropertyInfo> NodeProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => typeof(HlslTreeNode).IsAssignableFrom(ElementType(property))
                && BackingField(type, property) != null);
    }

    private static Type ElementType(PropertyInfo property)
    {
        return property.PropertyType.IsArray
            ? property.PropertyType.GetElementType()
            : property.PropertyType;
    }

    /// <summary>
    /// A statement with no constructor run, its lists empty and nothing else set.
    /// The slots read the properties they were given and nothing else, so this is
    /// enough to ask them what they hold - and it is the only way to reach the
    /// statements whose constructors want a shader's worth of context.
    ///
    /// The lists do have to be there: a switch reaches its labels through its cases,
    /// and a slot projecting over a null list would throw rather than report
    /// nothing.
    /// </summary>
    private static IStatement Empty(Type type)
    {
        var statement = (IStatement)RuntimeHelpers.GetUninitializedObject(type);
        foreach (PropertyInfo property in type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            FieldInfo field = BackingField(type, property);
            if (field != null && property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IList<>))
            {
                field.SetValue(statement, Activator.CreateInstance(
                    typeof(List<>).MakeGenericType(property.PropertyType.GenericTypeArguments)));
            }
        }
        return statement;
    }

    private static HlslTreeNode Sentinel(PropertyInfo property)
    {
        return (HlslTreeNode)RuntimeHelpers.GetUninitializedObject(ElementType(property));
    }

    private static void Set(IStatement statement, PropertyInfo property, HlslTreeNode node)
    {
        if (!property.PropertyType.IsArray)
        {
            SetField(statement, property, node);
            return;
        }
        Array one = Array.CreateInstance(ElementType(property), 1);
        one.SetValue(node, 0);
        SetField(statement, property, one);
    }

    // Through the backing field: most of these are read-only or init-only, which is
    // right for a statement the parser builds once and says nothing about whether a
    // slot reports them.
    private static void SetField(IStatement statement, PropertyInfo property, object value)
    {
        BackingField(statement.GetType(), property).SetValue(statement, value);
    }

    private static FieldInfo BackingField(Type type, PropertyInfo property)
    {
        return type.GetField($"<{property.Name}>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
