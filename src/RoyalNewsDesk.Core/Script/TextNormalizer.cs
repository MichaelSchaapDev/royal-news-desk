using System.Globalization;
using System.Text;

namespace RoyalNewsDesk.Core.Script;

/// <summary>
/// Cleans text before it reaches Piper, Rhubarb, and the subtitles, so all
/// three agree on exactly what was spoken.
/// </summary>
public static class TextNormalizer
{
    private static readonly Dictionary<string, string> Pronunciations = new(StringComparer.Ordinal)
    {
        // Piper reads these more naturally when spelled out.
        ["&"] = " and ",
        ["№"] = " number ",
    };

    public static string NormalizeParagraph(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormC);
        var builder = new StringBuilder(normalized.Length);

        foreach (var rune in normalized.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case 0x2018 or 0x2019: // curly single quotes
                    builder.Append('\'');
                    break;
                case 0x201C or 0x201D: // curly double quotes
                    builder.Append('"');
                    break;
                case 0x2026: // ellipsis
                    builder.Append("...");
                    break;
                case 0x2013 or 0x2014: // en/em dash: a comma reads as a newsreader pause
                    builder.Append(", ");
                    break;
                default:
                    if (IsEmojiLike(rune))
                    {
                        break;
                    }

                    builder.Append(rune.ToString());
                    break;
            }
        }

        var result = builder.ToString();
        foreach (var (from, to) in Pronunciations)
        {
            result = result.Replace(from, to, StringComparison.Ordinal);
        }

        // Collapse all whitespace runs (spaces, tabs), and ", ," artifacts
        // from dash handling. Splitting on null splits on any whitespace.
        result = string.Join(' ', result.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        result = result.Replace(" ,", ",", StringComparison.Ordinal);
        while (result.Contains(",,", StringComparison.Ordinal))
        {
            result = result.Replace(",,", ",", StringComparison.Ordinal);
        }

        return result.Trim();
    }

    /// <summary>Piper's prosody needs terminal punctuation on every sentence.</summary>
    public static string EnsureTerminalPunctuation(string sentence)
    {
        var trimmed = sentence.TrimEnd();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        var last = trimmed[^1];
        if (last is '.' or '!' or '?' or ':' or ';')
        {
            return trimmed;
        }

        if (last is '"' or '\'' && trimmed.Length >= 2 && trimmed[^2] is '.' or '!' or '?')
        {
            return trimmed;
        }

        return trimmed + ".";
    }

    private static bool IsEmojiLike(System.Text.Rune rune)
    {
        var category = System.Text.Rune.GetUnicodeCategory(rune);
        if (category == UnicodeCategory.OtherSymbol)
        {
            return true;
        }

        // Supplementary planes hold nearly all emoji and pictographs.
        return rune.Value >= 0x1F000;
    }
}
