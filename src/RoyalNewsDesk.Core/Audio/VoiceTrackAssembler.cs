using RoyalNewsDesk.Core.Script;
using RoyalNewsDesk.Core.Tts;
using RoyalNewsDesk.Core.Video;

namespace RoyalNewsDesk.Core.Audio;

/// <summary>Silences between speech, all in one place (seconds).</summary>
public sealed record GapPolicy(
    double LeadIn = 0.30,
    double BetweenSentences = 0.35,
    double BetweenParagraphs = 0.70,
    double BetweenSegments = 1.20,
    double TailOut = 1.00);

public sealed record AssembledVoice(
    string WavPath,
    double Duration,
    IReadOnlyList<SegmentTiming> Segments,
    IReadOnlyList<SentenceTiming> Sentences);

/// <summary>
/// Stitches the per-sentence wavs into one voice track by copying samples at
/// computed offsets. Sample-exact: the timeline is derived from the same
/// integers as the audio, so subtitles and lip-sync can never drift.
/// </summary>
public static class VoiceTrackAssembler
{
    public const double IntroDuration = 4.0;
    public const double OutroDuration = 8.0;

    public static AssembledVoice Assemble(
        SpeechPlan plan,
        IReadOnlyDictionary<int, TtsSentenceResult> spokenByOrdinal,
        string outputWavPath,
        GapPolicy? gaps = null)
    {
        gaps ??= new GapPolicy();
        const int rate = 22050;

        var chunks = new List<short[]>();
        var sentences = new List<SentenceTiming>();
        var segments = new List<SegmentTiming>();
        long cursor = AddSilence(chunks, rate, gaps.LeadIn);
        var ordinal = 0;

        for (var segmentIndex = 0; segmentIndex < plan.Segments.Count; segmentIndex++)
        {
            var segment = plan.Segments[segmentIndex];
            long segmentStartSamples = cursor;
            int? lastParagraph = null;
            var spokeAnything = false;

            foreach (var item in segment.Items)
            {
                switch (item)
                {
                    case SpeakPause pause:
                        cursor += AddSilence(chunks, rate, pause.Seconds);
                        break;

                    case SpeakSentence sentence:
                        if (spokeAnything)
                        {
                            var gap = lastParagraph == sentence.ParagraphIndex
                                ? gaps.BetweenSentences
                                : gaps.BetweenParagraphs;
                            cursor += AddSilence(chunks, rate, gap);
                        }

                        var spoken = spokenByOrdinal[ordinal];
                        var samples = WavFile.ReadMonoPcm16(spoken.WavPath);
                        chunks.Add(samples);
                        sentences.Add(new SentenceTiming(
                            segmentIndex,
                            ordinal,
                            sentence.Text,
                            IntroDuration + cursor / (double)rate,
                            IntroDuration + (cursor + samples.Length) / (double)rate));
                        cursor += samples.Length;
                        lastParagraph = sentence.ParagraphIndex;
                        spokeAnything = true;
                        ordinal++;
                        break;
                }
            }

            var segStart = IntroDuration + segmentStartSamples / (double)rate;
            var segEnd = IntroDuration + cursor / (double)rate;
            segments.Add(BuildSegmentTiming(plan.Segments[segmentIndex], segmentIndex, segStart, segEnd));

            if (segmentIndex < plan.Segments.Count - 1)
            {
                cursor += AddSilence(chunks, rate, gaps.BetweenSegments);
            }
        }

        cursor += AddSilence(chunks, rate, gaps.TailOut);

        var total = new short[cursor];
        long offset = 0;
        foreach (var chunk in chunks)
        {
            chunk.CopyTo(total, offset);
            offset += chunk.Length;
        }

        WavFile.WritePcm16(outputWavPath, rate, 1, total);
        return new AssembledVoice(outputWavPath, cursor / (double)rate, segments, sentences);
    }

    private static SegmentTiming BuildSegmentTiming(PlannedSegment segment, int index, double start, double end)
    {
        var ltStart = Timeline.FloorFrame(start + 0.5);
        var ltEnd = Timeline.CeilFrame(Math.Min(start + 8.0, end - 0.5));
        var panelStart = Timeline.FloorFrame(start + 0.4);
        var panelEnd = Timeline.CeilFrame(end - 0.5);
        return new SegmentTiming(
            index,
            segment.Headline,
            segment.ImageFile,
            start,
            end,
            ltStart,
            Math.Max(ltStart, ltEnd),
            panelStart,
            Math.Max(panelStart, panelEnd));
    }

    private static long AddSilence(List<short[]> chunks, int rate, double seconds)
    {
        var count = (long)Math.Round(seconds * rate);
        if (count > 0)
        {
            chunks.Add(new short[count]);
        }

        return count;
    }
}
