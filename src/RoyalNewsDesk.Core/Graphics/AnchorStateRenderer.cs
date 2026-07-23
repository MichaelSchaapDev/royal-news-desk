using RoyalNewsDesk.Core.LipSync;
using SkiaSharp;

namespace RoyalNewsDesk.Core.Graphics;

/// <summary>
/// Pre-renders the 18 anchor poses (9 mouths, eyes open or closed) as PNGs at
/// the exact on-screen size, composited from the layered SVG art.
/// </summary>
public static class AnchorStateRenderer
{
    public const int Width = 800;
    public const int Height = 980;

    /// <returns>Full paths by state name, e.g. "B_open" → ...\B_open.png.</returns>
    public static IReadOnlyDictionary<string, string> RenderAll(string anchorAssetsDir, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var basePath = Path.Combine(anchorAssetsDir, "base.svg");
        var eyesOpenPath = Path.Combine(anchorAssetsDir, "eyes-open.svg");
        var eyesClosedPath = Path.Combine(anchorAssetsDir, "eyes-closed.svg");

        using var rasterizer = new SvgRasterizer();
        var rendered = new Dictionary<string, string>(StringComparer.Ordinal);
        var target = new SKRect(0, 0, Width, Height);

        foreach (var mouth in Enum.GetValues<MouthShape>())
        {
            var mouthPath = Path.Combine(anchorAssetsDir, "mouth-" + mouth + ".svg");
            foreach (var eyesClosed in new[] { false, true })
            {
                using var surface = SKSurface.Create(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul));
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                rasterizer.Draw(canvas, basePath, target);
                rasterizer.Draw(canvas, eyesClosed ? eyesClosedPath : eyesOpenPath, target);
                rasterizer.Draw(canvas, mouthPath, target);

                var stateName = mouth + "_" + (eyesClosed ? "closed" : "open");
                var outputPath = Path.Combine(outputDir, stateName + ".png");
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.Create(outputPath);
                data.SaveTo(stream);
                rendered[stateName] = outputPath;
            }
        }

        return rendered;
    }
}
