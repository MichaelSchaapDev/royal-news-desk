namespace RoyalNewsDesk.Core.Models;

/// <summary>Channel identity applied to every rendered graphic.</summary>
public sealed class Branding
{
    public string ChannelName { get; set; } = "Royal News Desk";

    public string Tagline { get; set; } = "Separating royal fact from fiction";

    /// <summary>Main brand color as #RRGGBB.</summary>
    public string PrimaryColor { get; set; } = "#1B2A55";

    /// <summary>Accent color as #RRGGBB, used for highlights and the ticker block.</summary>
    public string AccentColor { get; set; } = "#C9A227";

    /// <summary>Optional path to a user-supplied logo image. Null uses the built-in logo.</summary>
    public string? LogoPath { get; set; }
}
