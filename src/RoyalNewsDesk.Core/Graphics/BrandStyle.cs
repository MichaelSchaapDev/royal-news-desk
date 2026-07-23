using RoyalNewsDesk.Core.Models;
using SkiaSharp;

namespace RoyalNewsDesk.Core.Graphics;

/// <summary>Branding resolved into drawable colors.</summary>
public sealed record BrandStyle(SKColor Primary, SKColor Accent, string ChannelName, string Tagline)
{
    public static readonly SKColor Ink = new(0x0A, 0x11, 0x22);
    public static readonly SKColor Paper = new(0xF7, 0xF5, 0xF2);

    public static BrandStyle From(Branding branding) => new(
        ParseHex(branding.PrimaryColor, new SKColor(0x1B, 0x2A, 0x55)),
        ParseHex(branding.AccentColor, new SKColor(0xC9, 0xA2, 0x27)),
        string.IsNullOrWhiteSpace(branding.ChannelName) ? "Royal News Desk" : branding.ChannelName.Trim(),
        branding.Tagline.Trim());

    public static SKColor ParseHex(string hex, SKColor fallback)
    {
        var text = hex.Trim().TrimStart('#');
        if (text.Length == 6 && uint.TryParse(
                text,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
        {
            return new SKColor(
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF));
        }

        return fallback;
    }

    /// <summary>A darker shade of the primary color for gradients.</summary>
    public SKColor PrimaryDark => new(
        (byte)(Primary.Red * 0.45),
        (byte)(Primary.Green * 0.45),
        (byte)(Primary.Blue * 0.45));
}
