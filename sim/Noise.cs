namespace Ashfall.Sim;

/// <summary>Deterministic 2D value noise with octaves. Same seed, same map, on every platform.</summary>
public sealed class ValueNoise
{
    private readonly int _seed;

    public ValueNoise(int seed) => _seed = seed;

    /// <summary>Hash an integer lattice point to [0, 1).</summary>
    public static double Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)x * 0x27d4eb2du;
            h = (h << 13) | (h >> 19);
            h ^= (uint)y * 0x165667b1u;
            h *= 0x9E3779B1u;
            h ^= h >> 15;
            h *= 0x85EBCA77u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / (double)0x1000000;
        }
    }

    private double Sample(double x, double y, int seed)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double fx = x - x0, fy = y - y0;
        double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        double a = Hash(x0, y0, seed), b = Hash(x0 + 1, y0, seed);
        double c = Hash(x0, y0 + 1, seed), d = Hash(x0 + 1, y0 + 1, seed);
        double top = a + (b - a) * sx;
        double bottom = c + (d - c) * sx;
        return top + (bottom - top) * sy;
    }

    /// <summary>Fractal noise in [0, 1).</summary>
    public double Fractal(double x, double y, int octaves = 4, double scale = 0.05)
    {
        double sum = 0, amp = 1, norm = 0, freq = scale;
        for (int i = 0; i < octaves; i++)
        {
            sum += Sample(x * freq, y * freq, _seed + i * 1013) * amp;
            norm += amp;
            amp *= 0.5;
            freq *= 2;
        }
        return sum / norm;
    }
}
