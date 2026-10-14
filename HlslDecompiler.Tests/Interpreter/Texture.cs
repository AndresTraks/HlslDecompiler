using System;

namespace HlslDecompiler.Tests.Interpreter;

/// <summary>
/// Stands in for a bound texture. Smooth, so a rounding difference in the
/// coordinate stays a rounding difference in the result.
/// </summary>
public static class Texture
{
    /// <param name="dimensions">Coordinates the texture reads; the rest of the
    /// register is garbage that may differ between the two programs.</param>
    public static float[] Sample(int sampler, float[] coordinates, int dimensions = 4)
    {
        float u = coordinates[0];
        float v = dimensions > 1 ? coordinates[1] : 0;
        float w = dimensions > 2 ? coordinates[2] : 0;
        float seed = sampler * 1.37f;
        return
        [
            Channel(u, v, w, seed + 0.0f),
            Channel(u, v, w, seed + 1.7f),
            Channel(u, v, w, seed + 3.1f),
            Channel(u, v, w, seed + 4.9f),
        ];
    }

    // In [0, 1] and never zero, so a sample used as a divisor stays finite.
    private static float Channel(float u, float v, float w, float seed)
    {
        float value = MathF.Sin(1.7f * u + 2.3f * v + 0.9f * w + seed)
            + MathF.Sin(0.7f * u - 1.1f * v + 1.3f * w + seed * 2);
        return 0.5f + 0.2f * value;
    }
}
