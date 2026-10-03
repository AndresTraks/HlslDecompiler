using HlslDecompiler.DirectXShaderModel;
using HlslDecompiler.Hlsl;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HlslDecompiler.Tests;

/// <summary>
/// What a rule costs, over the whole corpus, in one command.
///
///     HLSL_SWEEP=my-rule dotnet test --filter CostSweep
///
/// The rule reads <see cref="SweepFlags.Enabled"/>; this decompiles every shader
/// with it off and then on, and for every shader whose text changed it recompiles
/// both ways through fxc and reports the instruction counts side by side.
///
/// Only the changed ones reach fxc, and that is what makes a sweep worth running:
/// decompiling the corpus twice takes seconds, while an fxc call takes a third of a
/// second and a rule usually moves a handful of fixtures. The cost question only
/// arises where the text moved anyway.
///
/// It exists because the alternative is what kept happening: a rule gets reasoned
/// out from the one fixture that prompted it, and the nine or eleven other fixtures
/// it moved are discovered afterwards, one revert at a time. Three attempts in the
/// corpus history went that way. A sweep turns "does this rule pay?" into a
/// question with an answer.
///
/// Explicit, so a normal run never sees it.
/// </summary>
[TestFixture]
[Explicit("Run deliberately with HLSL_SWEEP set; it calls fxc and takes minutes.")]
public class CostSweepTests
{
    [Test]
    public void Sweep()
    {
        string[] flags = [.. SweepFlags.Named];
        if (flags.Length == 0)
        {
            Assert.Fail("Set HLSL_SWEEP to the name of the rule to measure, "
                + "e.g. HLSL_SWEEP=my-rule dotnet test --filter CostSweep");
        }
        if (RecompileTests.FxcPath == null)
        {
            Assert.Ignore("fxc.exe not found; a sweep is only instructions.");
        }

        string name = string.Join("+", flags);
        string folder = Path.Combine("CostSweep", string.Join("+", flags));
        var changed = new List<Change>();
        int shaders = 0;

        // Both ways, in one process. The flags come from the environment, so the
        // baseline is this build with them off - which is the shipped answer.
        foreach ((string profile, string baseFilename) in Corpus())
        {
            shaders++;
            string key = $"{profile}/{baseFilename}";
            string compiled = Path.Combine("CompiledShaders", profile, baseFilename + ".fxc");
            string baseline = Decompile(compiled, Path.Combine(folder, "base", profile,
                baseFilename + ".fx"), flags, enabled: false);
            string variant = Decompile(compiled, Path.Combine(folder, "variant", profile,
                baseFilename + ".fx"), flags, enabled: true);
            if (baseline == null || variant == null)
            {
                changed.Add(new Change(key, profile, Threw: true));
                continue;
            }
            if (File.ReadAllText(baseline) != File.ReadAllText(variant))
            {
                changed.Add(new Change(key, profile, Base: baseline, Variant: variant));
            }
        }

        var report = new StringBuilder();
        report.AppendLine($"Sweep: {name}");
        report.AppendLine($"{shaders} shaders decompiled both ways, "
            + $"{changed.Count} changed.");
        report.AppendLine();

        if (changed.Count == 0)
        {
            report.AppendLine("Nothing moved. Either the rule never fires, or it "
                + "fires and writes the same text - check that the flag is read "
                + "where it is meant to be.");
            Report(folder, report);
            return;
        }

        report.AppendLine($"{"fixture",-42}{"base",8}{"variant",9}{"delta",8}  note");
        int total = 0;
        int regressions = 0;
        foreach (Change change in changed)
        {
            if (change.Threw)
            {
                report.AppendLine($"{change.Key,-42}{"-",8}{"-",9}{"-",8}  "
                    + "DECOMPILER THREW");
                regressions++;
                continue;
            }
            int? before = Recompile(change.Profile, change.Base);
            int? after = Recompile(change.Profile, change.Variant);
            string note = RoundTripCostTests.KnownRegressions.ContainsKey(change.Key)
                ? "pinned" : string.Empty;
            if (before == null || after == null)
            {
                // A fixture that did not compile before is the recompile tier's
                // business; one that stops compiling now is this rule's.
                report.AppendLine($"{change.Key,-42}"
                    + $"{(before == null ? "no build" : before.ToString()),8}"
                    + $"{(after == null ? "no build" : after.ToString()),9}"
                    + $"{"-",8}  "
                    + (after == null && before != null
                        ? "STOPPED COMPILING" : "did not compile before"));
                if (after == null && before != null)
                {
                    regressions++;
                }
                continue;
            }
            int delta = after.Value - before.Value;
            total += delta;
            if (delta > 0)
            {
                regressions++;
                note = (note.Length == 0 ? string.Empty : note + ", ") + "REGRESSION";
            }
            report.AppendLine(
                $"{change.Key,-42}{before,8}{after,9}{Signed(delta),8}  {note}");
        }
        report.AppendLine();
        report.AppendLine($"{"",-42}{"",8}{"total",9}{Signed(total),8}"
            + $"  {regressions} regression(s)");
        report.AppendLine();
        foreach (Change change in changed.Where(c => !c.Threw))
        {
            report.AppendLine($"--- {change.Key}");
            report.Append(Diff(change.Base, change.Variant));
            report.AppendLine();
        }
        report.AppendLine("Full text both ways under " + folder + ".");
        Report(folder, report);
    }

    private static string Signed(int delta)
    {
        return delta > 0 ? $"+{delta}" : delta.ToString();
    }

    /// <summary>
    /// What changed, as the lines between the common prefix and the common suffix.
    /// Not a least-edit diff: a rule that moves two places in one shader has the
    /// lines between them reported as well, which is more to read and never less
    /// than the truth. The point is to have the answer and the evidence in one file
    /// rather than a folder to go and look in.
    /// </summary>
    private static string Diff(string baseline, string variant)
    {
        string[] before = File.ReadAllLines(baseline);
        string[] after = File.ReadAllLines(variant);
        int head = 0;
        while (head < before.Length && head < after.Length && before[head] == after[head])
        {
            head++;
        }
        int tail = 0;
        while (tail < before.Length - head && tail < after.Length - head
            && before[before.Length - 1 - tail] == after[after.Length - 1 - tail])
        {
            tail++;
        }
        var diff = new StringBuilder();
        foreach (string line in before.Skip(head).Take(before.Length - head - tail))
        {
            diff.AppendLine($"  - {line.Trim()}");
        }
        foreach (string line in after.Skip(head).Take(after.Length - head - tail))
        {
            diff.AppendLine($"  + {line.Trim()}");
        }
        return diff.ToString();
    }

    /// <summary>
    /// The corpus, once each. RecompileTests.Shaders() is the list the recompile and
    /// cost tiers run on, which is the list a cost question is asked about.
    /// </summary>
    private static IEnumerable<(string Profile, string BaseFilename)> Corpus()
    {
        return RecompileTests.Shaders()
            .Select(data => ((string)data.Arguments[0], (string)data.Arguments[1]))
            .Distinct();
    }

    /// <summary>
    /// The decompiled text, or null if the decompiler threw - which is a result and
    /// not a reason to stop: a rule that crashes on one shader should say so beside
    /// what it did to the others.
    /// </summary>
    private static string Decompile(
        string compiled, string output, string[] flags, bool enabled)
    {
        foreach (string flag in flags)
        {
            SweepFlags.Override(flag, enabled);
        }
        try
        {
            ShaderModel shader = RecompileTests.ReadShaderModel(compiled);
            FileUtil.MakeFolder(output);
            new HlslAstWriter(shader).Write(output);
            return output;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            SweepFlags.Reset();
        }
    }

    /// <summary>The instruction count fxc gives this text, or null if it does not
    /// compile.</summary>
    private static int? Recompile(string profile, string hlsl)
    {
        string binary = Path.ChangeExtension(hlsl, ".fxo");
        return RecompileTests.RunFxc(profile, hlsl, binary) == null
            ? RoundTripCostTests.CountInstructions(binary)
            : null;
    }

    private static void Report(string folder, StringBuilder report)
    {
        string text = report.ToString();
        TestContext.Out.Write(text);
        Console.Write(text);
        string file = Path.Combine(folder, "sweep.txt");
        FileUtil.MakeFolder(file);
        File.WriteAllText(file, text);
    }

    private record Change(
        string Key,
        string Profile,
        string Base = null,
        string Variant = null,
        bool Threw = false);
}
