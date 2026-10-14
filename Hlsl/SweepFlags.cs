using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Temporary switches for a rule under measurement, so one build can decompile both
/// ways: a rule asks <c>SweepFlags.Enabled("name")</c>. All off by default. A flag
/// is removed once its rule is decided, so having no callers is normal.
/// </summary>
public static class SweepFlags
{
    /// <summary>
    /// Comma-separated HLSL_SWEEP, for command-line decompiles.
    /// </summary>
    private static readonly HashSet<string> FromEnvironment =
        new((Environment.GetEnvironmentVariable("HLSL_SWEEP") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> Set =
        new(FromEnvironment, StringComparer.OrdinalIgnoreCase);

    public static bool Enabled(string name)
    {
        return Set.Contains(name);
    }

    /// <summary>
    /// Lets one process decompile with a flag off and then on.
    /// </summary>
    public static void Override(string name, bool enabled)
    {
        if (enabled)
        {
            Set.Add(name);
        }
        else
        {
            Set.Remove(name);
        }
    }

    /// <summary>Back to what the environment asked for.</summary>
    public static void Reset()
    {
        Set.Clear();
        foreach (string name in FromEnvironment)
        {
            Set.Add(name);
        }
    }

    /// <summary>The flags the environment named, for a sweep with no name given.</summary>
    public static IEnumerable<string> Named => FromEnvironment.OrderBy(name => name);
}
