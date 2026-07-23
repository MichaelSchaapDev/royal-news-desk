using SkiaSharp;
using Svg.Skia;

namespace RoyalNewsDesk.Core.Graphics;

/// <summary>Loads SVG assets once and rasterizes them at exact pixel sizes.</summary>
public sealed class SvgRasterizer : IDisposable
{
    private readonly Dictionary<string, SKSvg> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SKSvg Load(string path)
    {
        if (_cache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        var svg = new SKSvg();
        if (svg.Load(path) is null)
        {
            svg.Dispose();
            throw new InvalidDataException("Could not load SVG: " + path);
        }

        _cache[path] = svg;
        return svg;
    }

    /// <summary>Draws an SVG scaled to fill the given rectangle of the canvas.</summary>
    public void Draw(SKCanvas canvas, string path, SKRect target)
    {
        var svg = Load(path);
        var picture = svg.Picture ?? throw new InvalidDataException("SVG has no picture: " + path);
        var bounds = picture.CullRect;

        canvas.Save();
        canvas.Translate(target.Left, target.Top);
        canvas.Scale(target.Width / bounds.Width, target.Height / bounds.Height);
        canvas.DrawPicture(picture);
        canvas.Restore();
    }

    public void Dispose()
    {
        foreach (var svg in _cache.Values)
        {
            svg.Dispose();
        }

        _cache.Clear();
    }
}
