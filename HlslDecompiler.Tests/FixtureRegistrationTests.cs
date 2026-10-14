using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HlslDecompiler.Tests;

/// <summary>
/// Every shader in CompiledShaders has a [TestCase] in a text-diff decompile test;
/// the fxc tiers enumerate the folder, but those tests are explicit lists.
/// zz_ shaders are exempt: they are temporary probes.
/// </summary>
[TestFixture]
public class FixtureRegistrationTests
{
    [Test]
    public void EveryCompiledShaderIsTextCompared()
    {
        // The same enumeration the fxc tiers run.
        List<string> corpus = [.. RecompileTests.Shaders()
            .Select(data => $"{data.Arguments[0]}/{data.Arguments[1]}")
            .Where(name => !name.Contains("/zz_"))
            .OrderBy(name => name)];
        List<string> registered = [.. RegisteredCases().OrderBy(name => name)];

        Assert.That(corpus.Except(registered), Is.Empty,
            "In CompiledShaders and compared by neither DecompileTest nor "
            + "DecompileShaderTest. Add a [TestCase] for it, or the .asm and .fx "
            + "beside it are read by nothing.");
        Assert.That(registered.Except(corpus), Is.Empty,
            "Named by a [TestCase] with no .fxc in CompiledShaders.");
    }

    // Read off the attributes, not listed again.
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
