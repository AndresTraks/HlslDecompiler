using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HlslDecompiler.Tests;

/// <summary>
/// That every compiled shader in the corpus is text-compared by one of the two
/// decompile tests.
///
/// A fixture is four stored files and a line of C#, and the line is the part with
/// nothing to catch it missing. The fxc-backed tiers enumerate CompiledShaders, so
/// they pick a new shader up on their own; the test that compares the decompiled
/// text against the stored .asm and .fx is a list of [TestCase] attributes, and a
/// shader left off it is simply not compared. The four files sit there looking like
/// a fixture and nothing reads three of them.
///
/// Nothing had gone missing when this was written - 499 shaders, 499 registered -
/// which is the reason to write it down while that is still true rather than after
/// a fixture has been quietly asleep for a year.
/// </summary>
[TestFixture]
public class FixtureRegistrationTests
{
    [Test]
    public void EveryCompiledShaderIsTextCompared()
    {
        // The same enumeration the fxc tiers run, so what this checks is exactly
        // "everything those tiers decompile is also compared as text".
        List<string> corpus = [.. RecompileTests.Shaders()
            .Select(data => $"{data.Arguments[0]}/{data.Arguments[1]}")
            .OrderBy(name => name)];
        List<string> registered = [.. RegisteredCases().OrderBy(name => name)];

        Assert.That(corpus.Except(registered), Is.Empty,
            "In CompiledShaders and compared by neither DecompileTest nor "
            + "DecompileShaderTest. Add a [TestCase] for it, or the .asm and .fx "
            + "beside it are read by nothing.");
        Assert.That(registered.Except(corpus), Is.Empty,
            "Named by a [TestCase] with no .fxc in CompiledShaders.");
    }

    /// <summary>
    /// The shaders the two text-diff tests name, read off their attributes rather
    /// than listed again here - a second list would be the thing this test is about.
    /// </summary>
    private static IEnumerable<string> RegisteredCases()
    {
        foreach (MethodInfo test in new[]
        {
            typeof(DecompileDxbcTests).GetMethod(nameof(DecompileDxbcTests.DecompileTest)),
            typeof(DecompileTests).GetMethod(nameof(DecompileTests.DecompileShaderTest)),
        })
        {
            foreach (TestCaseAttribute testCase in test.GetCustomAttributes<TestCaseAttribute>())
            {
                yield return $"{testCase.Arguments[0]}/{testCase.Arguments[1]}";
            }
        }
    }
}
