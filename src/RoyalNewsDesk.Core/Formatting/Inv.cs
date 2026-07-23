using System.Globalization;

namespace RoyalNewsDesk.Core.Formatting;

/// <summary>
/// Invariant-culture formatting helpers. Every number that ends up inside an
/// ffmpeg argument, filter graph, ffconcat file, or subtitle file MUST go
/// through these. The target machine runs a Dutch locale, where plain
/// ToString() turns 0.5 into "0,5" and corrupts all of the above.
/// </summary>
public static class Inv
{
    /// <summary>Formats an interpolated string with the invariant culture.</summary>
    public static string F(FormattableString value) => FormattableString.Invariant(value);

    /// <summary>Seconds with millisecond precision: 1.5 → "1.500".</summary>
    public static string N3(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    /// <summary>Integer without group separators.</summary>
    public static string I(long value) => value.ToString(CultureInfo.InvariantCulture);
}
