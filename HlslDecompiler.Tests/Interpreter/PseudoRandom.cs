using System.Text;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Values hashed from a name, not drawn from a sequence, so request order does not
/// matter and failures reproduce.
/// </summary>
public static class PseudoRandom
{
    public static float[] Vector(string name, int seed = 0)
    {
        uint hash = Hash(name) ^ (uint)(seed * 2654435761);
        return
        [
            Component(hash, 0),
            Component(hash, 1),
            Component(hash, 2),
            Component(hash, 3),
        ];
    }

    // In [-2, 2]: small so products stay comparable, signed so a lost negation shows.
    // Rounded once, from a double: in floats, taking 2 away cleared the low mantissa
    // bits, so a test of a float's low bits (`id & 1`) never went the other way.
    private static float Component(uint hash, int index)
    {
        uint bits = Mix(hash + (uint)index * 0x9E3779B9);
        return (float)((bits % 40001) / 10000.0 - 2.0);
    }

    /// <summary>
    /// A drawn value rounded to -2, -1, 1 or 2, so two values are often equal. Not
    /// 0: a ±0 input divided by takes the infinity's sign from the zero's, which
    /// two correct compilations need not agree on, and even +0 makes a projective
    /// w of 0, whose NaN coordinates ps_1_4's cnd and the `>=` it is written as
    /// branch on differently. (Computed zeros still occur.) So `x >= 0` against a
    /// constant still never meets its boundary.
    /// </summary>
    public static float Rounded(float value)
    {
        float rounded = System.MathF.Round(value);
        return rounded == 0 ? 1 : rounded;
    }

    private static uint Hash(string value)
    {
        uint hash = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes(value))
        {
            hash = (hash ^ b) * 16777619;
        }
        return hash;
    }

    private static uint Mix(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352D;
        value ^= value >> 15;
        value *= 0x846CA68B;
        value ^= value >> 16;
        return value;
    }
}
