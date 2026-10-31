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
/// The cost of a rule gated on <see cref="SweepFlags.Enabled"/>:
///
///     HLSL_SWEEP=my-rule dotnet test --filter CostSweep
///
/// Decompiles every shader with the flag off and on, and recompiles only those
/// whose text changed through fxc, reporting instruction counts side by side.
/// </summary>
[TestFixture]
[Explicit("Run deliberately with HLSL_SWEEP set; it calls fxc and takes minutes.")]
// SweepFlags is process-global.
[NonParallelizable]
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
        if (!ShaderCompiler.IsAvailable)
        {
            Assert.Ignore("No HLSL compiler; a sweep is only instructions.");
        }

        string name = string.Join("+", flags);
        string folder = Path.Combine("CostSweep", string.Join("+", flags));
        var changed = new List<Change>();
        var wrong = new List<string>();
        int shaders = 0;

        // The baseline is this build with the flags off.
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
            string computes = Differs(change);
            if (computes != null)
            {
                note = (note.Length == 0 ? string.Empty : note + ", ") + "COMPUTES DIFFERENTLY";
                wrong.Add($"{change.Key}: {computes}");
                regressions++;
            }
            if (before == null || after == null)
            {
                // Only one that stops compiling is this rule's regression.
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
        if (wrong.Count != 0)
        {
            report.AppendLine("What the rule changed about the answers:");
            foreach (string difference in wrong)
            {
                report.AppendLine("  " + difference);
            }
            report.AppendLine();
        }
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

    // The lines between the common prefix and suffix; not a minimal diff.
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

    private static IEnumerable<(string Profile, string BaseFilename)> Corpus()
    {
        return RecompileTests.Shaders()
            .Select(data => ((string)data.Arguments[0], (string)data.Arguments[1]))
            .Distinct();
    }

    // Null if the decompiler threw; reported, not fatal.
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

    private static int? Recompile(string profile, string hlsl)
    {
        string binary = Path.ChangeExtension(hlsl, ".fxo");
        return RecompileTests.RunFxc(profile, hlsl, binary) == null
            ? RoundTripCostTests.CountInstructions(binary)
            : null;
    }

    /// <summary>
    /// How the variant computes differently from the baseline (not the original),
    /// or null. A wrong rewrite can look cheaper, so counts alone are not enough.
    /// </summary>
    private static string Differs(Change change)
    {
        string baseBinary = Path.ChangeExtension(change.Base, ".fxo");
        string variantBinary = Path.ChangeExtension(change.Variant, ".fxo");
        if (!File.Exists(baseBinary) || !File.Exists(variantBinary))
        {
            return null;
        }
        try
        {
            string[] differences = [.. EquivalenceTests.CompareRuns(
                RecompileTests.ReadShaderModel(baseBinary),
                RecompileTests.ReadShaderModel(variantBinary),
                "the baseline",
                "the variant")];
            return differences.Length == 0 ? null : differences[0];
        }
        catch (Exception e)
        {
            // Not silent: an unrun check must not read as coverage.
            return $"not run - {e.GetType().Name}: {e.Message}";
        }
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
