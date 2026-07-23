using SkiaSharp;

namespace RoyalNewsDesk.Core.Graphics;

/// <summary>Small text helpers: fitting, wrapping, centered drawing.</summary>
public static class TextLayout
{
    /// <summary>Largest size from <paramref name="startSize"/> down at which the text fits the width.</summary>
    public static float FitSize(SKTypeface typeface, string text, float maxWidth, float startSize, float minSize)
    {
        for (var size = startSize; size > minSize; size -= 2)
        {
            using var font = new SKFont(typeface, size);
            if (font.MeasureText(text) <= maxWidth)
            {
                return size;
            }
        }

        return minSize;
    }

    /// <summary>
    /// Wraps into at most <paramref name="maxLines"/> lines, shrinking the font
    /// until everything fits. Returns the lines and the size used.
    /// </summary>
    public static (IReadOnlyList<string> Lines, float Size) WrapToWidth(
        SKTypeface typeface,
        string text,
        float maxWidth,
        float startSize,
        float minSize,
        int maxLines)
    {
        for (var size = startSize; size >= minSize; size -= 2)
        {
            using var font = new SKFont(typeface, size);
            var lines = Wrap(font, text, maxWidth);
            if (lines.Count <= maxLines)
            {
                return (lines, size);
            }
        }

        using var smallest = new SKFont(typeface, minSize);
        var clipped = Wrap(smallest, text, maxWidth);
        return (clipped.Take(maxLines).ToList(), minSize);
    }

    public static void DrawCentered(SKCanvas canvas, string text, float centerX, float baselineY, SKFont font, SKPaint paint)
    {
        var width = font.MeasureText(text);
        canvas.DrawText(text, centerX - width / 2, baselineY, SKTextAlign.Left, font, paint);
    }

    private static List<string> Wrap(SKFont font, string text, float maxWidth)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var current = "";
        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (font.MeasureText(candidate) <= maxWidth || current.Length == 0)
            {
                current = candidate;
            }
            else
            {
                lines.Add(current);
                current = word;
            }
        }

        if (current.Length > 0)
        {
            lines.Add(current);
        }

        return lines;
    }
}
