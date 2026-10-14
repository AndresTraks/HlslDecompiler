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
/// Nodes a statement holds outside its input/output maps are not graph edges and
/// are reached only via IStatement.HeldSlots. A node field without a slot would be
/// silently skipped by every walk, so each one must be slotted or excluded here.
/// </summary>
[TestFixture]
public class HeldSlotTests
{
    // Node-typed members that are not compiled as expressions.
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

    // A named slot's setter must write the place its getter reads; unnamed slots
    // are written out again and must not move.
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

    // Labels hang off the cases, so the reflection tests above miss them.
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

    // Auto-properties only (the sentinel goes in via the backing field); a node
    // behind a hand-written property would go unchecked.
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

    // No constructor run (some need a whole shader); lists are created empty so
    // slots projecting over them do not throw.
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

    // Via the backing field: most are read-only or init-only.
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
