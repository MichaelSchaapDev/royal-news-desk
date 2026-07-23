using SkiaSharp;

namespace RoyalNewsDesk.Core.Graphics;

/// <summary>The bundled OFL fonts, loaded once. IBM Plex Serif for headlines, Plex Sans for the rest.</summary>
public sealed class FontCatalog : IDisposable
{
    public FontCatalog(string fontsDir)
    {
        HeadlineBold = Load(fontsDir, "IBMPlexSerif-Bold.ttf");
        HeadlineMedium = Load(fontsDir, "IBMPlexSerif-Medium.ttf");
        SansRegular = Load(fontsDir, "IBMPlexSans-Regular.ttf");
        SansSemiBold = Load(fontsDir, "IBMPlexSans-SemiBold.ttf");
        SansBold = Load(fontsDir, "IBMPlexSans-Bold.ttf");
    }

    public SKTypeface HeadlineBold { get; }

    public SKTypeface HeadlineMedium { get; }

    public SKTypeface SansRegular { get; }

    public SKTypeface SansSemiBold { get; }

    public SKTypeface SansBold { get; }

    private static SKTypeface Load(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        return SKTypeface.FromFile(path)
            ?? throw new InvalidDataException("Could not load font: " + path);
    }

    public void Dispose()
    {
        HeadlineBold.Dispose();
        HeadlineMedium.Dispose();
        SansRegular.Dispose();
        SansSemiBold.Dispose();
        SansBold.Dispose();
    }
}
