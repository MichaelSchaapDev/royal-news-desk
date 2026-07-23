namespace RoyalNewsDesk.Core.LipSync;

/// <summary>
/// If Rhubarb dies, the episode still renders: alternate simple mouth shapes
/// while sentences play and rest in the gaps. Looks approximate, never broken.
/// </summary>
public static class FallbackVisemes
{
    private const double StepSeconds = 0.12;

    public static MouthCueTrack FromSentences(
        IEnumerable<(double Start, double End)> sentences,
        double totalDuration)
    {
        var cues = new List<MouthCue>();
        MouthShape[] cycle = [MouthShape.B, MouthShape.C, MouthShape.B, MouthShape.E];
        var step = 0;

        foreach (var (start, end) in sentences)
        {
            var time = start;
            while (time < end)
            {
                var next = Math.Min(end, time + StepSeconds);
                cues.Add(new MouthCue(time, next, cycle[step % cycle.Length]));
                step++;
                time = next;
            }
        }

        return new MouthCueTrack(totalDuration, cues);
    }
}
