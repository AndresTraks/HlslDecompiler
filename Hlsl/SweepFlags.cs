using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Switches for a rule being measured, so that one build can decompile the corpus
/// both ways and the cost sweep can say what the rule is worth.
///
/// Nothing in the decompiler's own behaviour depends on this: every flag is off
/// unless something turns it on, so the shipped answer is the baseline and a sweep
/// measures the variant against it. A rule under test asks
/// <c>SweepFlags.Enabled("name")</c> and the sweep does the rest.
///
/// Why this is here rather than in the tests: the thing that stopped sweeps from
/// being run was that measuring both ways meant editing a rule, rebuilding,
/// decompiling, writing the numbers down, reverting and doing it again - at which
/// point a rule gets judged on the one fixture that prompted it. Three attempts in
/// the corpus history were reverted for exactly that, each after moving nine or
/// eleven fixtures nobody had looked at. A flag is a small price for the question
/// being cheap to ask.
///
/// A flag is temporary by intention. It goes in while a rule is being measured and
/// comes out with the decision - either the rule ships and the flag is deleted, or
/// the rule does not and both go. So having no callers at all is the normal state,
/// and means only that nothing is being measured today. The first rule measured this
/// way was a naming rule reported under the ps_5_0/split_transform entry in
/// RoundTripCostTests; it did not ship, and its flag went with it.
/// </summary>
public static class SweepFlags
{
    /// <summary>
    /// The flags named in HLSL_SWEEP, comma separated, read once. This is how a
    /// decompile from the command line gets the variant - the sweep itself sets them
    /// directly, since it runs both ways in one process.
    /// </summary>
    private static readonly HashSet<string> FromEnvironment =
        new((Environment.GetEnvironmentVariable("HLSL_SWEEP") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> Set =
        new(FromEnvironment, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the rule under this name is to be taken.</summary>
    public static bool Enabled(string name)
    {
        return Set.Contains(name);
    }

    /// <summary>
    /// Turns a flag on or off for what follows. The sweep decompiles each shader
    /// with the flag off and then on, in one process, so that a run costs one
    /// decompile each way rather than two processes and two reads of the corpus.
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

    /// <summary>Back to what the environment asked for, which is what a sweep
    /// leaves behind it.</summary>
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
