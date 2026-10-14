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
    private static float Component(uint hash, int index)
    {
        uint bits = Mix(hash + (uint)index * 0x9E3779B9);
        return (bits % 40001) / 10000f - 2f;
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
