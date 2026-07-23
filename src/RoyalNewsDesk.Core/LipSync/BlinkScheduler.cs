namespace RoyalNewsDesk.Core.LipSync;

/// <summary>
/// Deterministic eye blinks: same episode title, same blinks, so re-renders
/// are reproducible. Blinks last three frames at 25 fps.
/// </summary>
public static class BlinkScheduler
{
    public const double BlinkSeconds = 0.12;
    private const double MinGap = 2.4;
    private const double MaxGap = 5.2;

    public static IReadOnlyList<(double Start, double End)> Schedule(string seedText, double duration)
    {
        var blinks = new List<(double, double)>();
        var state = Fnv1a(seedText);
        var time = NextDouble(ref state) * MaxGap + 1.0;

        while (time + BlinkSeconds < duration)
        {
            blinks.Add((time, time + BlinkSeconds));
            time += MinGap + NextDouble(ref state) * (MaxGap - MinGap);
        }

        return blinks;
    }

    private static ulong Fnv1a(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return hash == 0 ? 1UL : hash;
    }

    /// <summary>Small deterministic PRNG (xorshift64*), stable across .NET versions.</summary>
    private static double NextDouble(ref ulong state)
    {
        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        var value = state * 2685821657736338717UL;
        return (value >> 11) * (1.0 / (1UL << 53));
    }
}
