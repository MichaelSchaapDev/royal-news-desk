using System.Text;
using RoyalNewsDesk.Core.Formatting;

namespace RoyalNewsDesk.Core.LipSync;

/// <summary>One anchor pose held for a run of whole frames.</summary>
public sealed record AnchorInterval(MouthShape Mouth, bool EyesClosed, int Frames)
{
    public string StateName => Mouth + "_" + (EyesClosed ? "closed" : "open");

    public string FileName => StateName + ".png";
}

/// <summary>
/// Merges mouth cues and blinks into a frame-exact pose list, then writes it
/// as an ffconcat stills sequence. This is what makes the anchor render at
/// almost zero cost: ffmpeg shows one PNG per pose instead of 25 frames a
/// second being drawn by us.
/// </summary>
public static class AnchorTimelineBuilder
{
    public const int Fps = 25;

    public static IReadOnlyList<AnchorInterval> Build(
        MouthCueTrack mouths,
        IReadOnlyList<(double Start, double End)> blinks,
        double duration)
    {
        var totalFrames = Math.Max(1, (int)Math.Round(duration * Fps, MidpointRounding.AwayFromZero));

        // Collect every moment a pose can change, snapped to whole frames.
        var boundaryFrames = new SortedSet<int> { 0, totalFrames };
        foreach (var cue in mouths.Cues)
        {
            AddFrame(boundaryFrames, cue.Start, totalFrames);
            AddFrame(boundaryFrames, cue.End, totalFrames);
        }

        foreach (var (start, end) in blinks)
        {
            AddFrame(boundaryFrames, start, totalFrames);
            AddFrame(boundaryFrames, end, totalFrames);
        }

        var frames = boundaryFrames.ToList();
        var intervals = new List<AnchorInterval>();
        for (var i = 0; i < frames.Count - 1; i++)
        {
            var frameCount = frames[i + 1] - frames[i];
            if (frameCount <= 0)
            {
                continue;
            }

            var midpoint = (frames[i] + frames[i + 1]) / 2.0 / Fps;
            var mouth = mouths.ShapeAt(midpoint);
            var eyesClosed = blinks.Any(b => midpoint >= b.Start && midpoint < b.End);

            if (intervals.Count > 0
                && intervals[^1].Mouth == mouth
                && intervals[^1].EyesClosed == eyesClosed)
            {
                intervals[^1] = intervals[^1] with { Frames = intervals[^1].Frames + frameCount };
            }
            else
            {
                intervals.Add(new AnchorInterval(mouth, eyesClosed, frameCount));
            }
        }

        return intervals;
    }

    /// <summary>
    /// Writes the ffconcat list with bare file names; it must sit in the same
    /// folder as the PNGs. The final file repeats without a duration because
    /// the concat demuxer's handling of the last duration is unreliable.
    /// </summary>
    public static void WriteFfconcat(string path, IReadOnlyList<AnchorInterval> intervals)
    {
        var text = new StringBuilder();
        text.Append("ffconcat version 1.0\n");
        foreach (var interval in intervals)
        {
            text.Append("file '").Append(interval.FileName).Append("'\n");
            text.Append("duration ").Append(Inv.N3(interval.Frames / (double)Fps)).Append('\n');
        }

        if (intervals.Count > 0)
        {
            text.Append("file '").Append(intervals[^1].FileName).Append("'\n");
        }

        File.WriteAllText(path, text.ToString());
    }

    private static void AddFrame(SortedSet<int> frames, double time, int totalFrames)
    {
        var frame = (int)Math.Round(time * Fps, MidpointRounding.AwayFromZero);
        if (frame > 0 && frame < totalFrames)
        {
            frames.Add(frame);
        }
    }
}
