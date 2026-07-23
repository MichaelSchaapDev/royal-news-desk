using RoyalNewsDesk.Core.Models;

namespace RoyalNewsDesk.Core.Script;

/// <summary>
/// Splits a paragraph into sentences for per-sentence TTS. Kept in C# (not
/// left to Piper) because exact per-sentence timings drive the subtitles.
/// </summary>
public static class SentenceSplitter
{
    private const int MaxSentenceLength = 450;

    private static readonly string[] ProtectedAbbreviations =
    [
        "Mr.", "Mrs.", "Ms.", "Dr.", "Prof.", "St.", "Sgt.", "Maj.", "Gen.",
        "Rev.", "Hon.", "H.R.H.", "H.M.", "e.g.", "i.e.", "etc.", "No.",
        "vs.", "approx.",
    ];

    public static List<string> Split(string paragraph, int lineNumber, List<PipelineWarning> warnings)
    {
        var sentences = new List<string>();
        var start = 0;
        for (var i = 0; i < paragraph.Length; i++)
        {
            var c = paragraph[i];
            if (c is not ('.' or '!' or '?'))
            {
                continue;
            }

            // Swallow closing quotes and brackets right after the terminator.
            var end = i;
            while (end + 1 < paragraph.Length && paragraph[end + 1] is '"' or '\'' or ')' or ']')
            {
                end++;
            }

            if (!IsSentenceBoundary(paragraph, i, end))
            {
                continue;
            }

            var sentence = paragraph[start..(end + 1)].Trim();
            if (sentence.Length > 0)
            {
                AddWithLengthGuard(sentence, lineNumber, sentences, warnings);
            }

            start = end + 1;
            i = end;
        }

        var tail = paragraph[start..].Trim();
        if (tail.Length > 0)
        {
            AddWithLengthGuard(tail, lineNumber, sentences, warnings);
        }

        return sentences;
    }

    private static bool IsSentenceBoundary(string text, int dotIndex, int endIndex)
    {
        // End of paragraph is always a boundary.
        if (endIndex + 1 >= text.Length)
        {
            return true;
        }

        // Boundary needs whitespace, then an uppercase letter, digit, or quote.
        if (!char.IsWhiteSpace(text[endIndex + 1]))
        {
            return false;
        }

        var next = endIndex + 1;
        while (next < text.Length && char.IsWhiteSpace(text[next]))
        {
            next++;
        }

        if (next >= text.Length)
        {
            return true;
        }

        if (!(char.IsUpper(text[next]) || char.IsDigit(text[next]) || text[next] is '"' or '\''))
        {
            return false;
        }

        if (text[dotIndex] is '!' or '?')
        {
            return true;
        }

        // Decimal numbers: "2.5" never splits (no whitespace) so only guards remain.
        foreach (var abbreviation in ProtectedAbbreviations)
        {
            if (EndsWithToken(text, dotIndex, abbreviation))
            {
                return false;
            }
        }

        // Single-capital initials: "J. Smith".
        if (dotIndex >= 1 && char.IsUpper(text[dotIndex - 1])
            && (dotIndex == 1 || !char.IsLetter(text[dotIndex - 2])))
        {
            return false;
        }

        return true;
    }

    private static bool EndsWithToken(string text, int dotIndex, string abbreviation)
    {
        var start = dotIndex - abbreviation.Length + 1;
        if (start < 0 || string.CompareOrdinal(text, start, abbreviation, 0, abbreviation.Length) != 0)
        {
            return false;
        }

        return start == 0 || !char.IsLetterOrDigit(text[start - 1]);
    }

    private static void AddWithLengthGuard(
        string sentence,
        int lineNumber,
        List<string> sentences,
        List<PipelineWarning> warnings)
    {
        if (sentence.Length <= MaxSentenceLength)
        {
            sentences.Add(sentence);
            return;
        }

        // Force a split near the midpoint at a clause boundary; protects
        // Piper's prosody and keeps subtitle chunks sane.
        warnings.Add(new PipelineWarning("W501", lineNumber, sentence[..60] + "..."));
        var middle = sentence.Length / 2;
        var breakAt = BestBreak(sentence, middle);
        sentences.Add(sentence[..breakAt].Trim());
        AddWithLengthGuard(sentence[breakAt..].Trim(), lineNumber, sentences, warnings);
    }

    private static int BestBreak(string sentence, int middle)
    {
        for (var distance = 0; distance < middle - 10; distance++)
        {
            foreach (var candidate in new[] { middle - distance, middle + distance })
            {
                if (candidate > 10 && candidate < sentence.Length - 10
                    && sentence[candidate] == ' '
                    && sentence[candidate - 1] is ';' or ',')
                {
                    return candidate;
                }
            }
        }

        var space = sentence.IndexOf(' ', middle);
        return space > 0 ? space : middle;
    }
}
