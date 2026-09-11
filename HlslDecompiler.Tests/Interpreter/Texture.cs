using System;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Stands in for a bound texture. Smooth rather than noisy, so that a coordinate
/// that differs in its last bits gives a result that differs in its last bits -
/// a lookup table would turn rounding into a large difference and drown out the
/// real ones.
/// </summary>
public static class Texture
{
    public static float[] Sample(int sampler, float[] coordinates)
    {
        float u = coordinates[0];
        float v = coordinates[1];
        float w = coordinates[2];
        float seed = sampler * 1.37f;
        return
        [
            Channel(u, v, w, seed + 0.0f),
            Channel(u, v, w, seed + 1.7f),
            Channel(u, v, w, seed + 3.1f),
            Channel(u, v, w, seed + 4.9f),
        ];
    }

    // Bounded to [0, 1] like a real texture read, and never zero, so that a
    // divisor taken from a sample does not blow up.
    private static float Channel(float u, float v, float w, float seed)
    {
        float value = MathF.Sin(1.7f * u + 2.3f * v + 0.9f * w + seed)
            + MathF.Sin(0.7f * u - 1.1f * v + 1.3f * w + seed * 2);
        return 0.5f + 0.2f * value;
    }
}
