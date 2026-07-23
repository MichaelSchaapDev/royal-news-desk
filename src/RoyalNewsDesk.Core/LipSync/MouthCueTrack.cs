namespace RoyalNewsDesk.Core.LipSync;

/// <summary>Rhubarb's nine mouth shapes. X is the rest pose.</summary>
public enum MouthShape
{
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    X,
}

/// <summary>One mouth position on the voice-track timeline (seconds).</summary>
public sealed record MouthCue(double Start, double End, MouthShape Shape);

public sealed record MouthCueTrack(double Duration, IReadOnlyList<MouthCue> Cues)
{
    /// <summary>The shape active at a moment; X when between cues.</summary>
    public MouthShape ShapeAt(double time)
    {
        foreach (var cue in Cues)
        {
            if (time >= cue.Start && time < cue.End)
            {
                return cue.Shape;
            }

            if (cue.Start > time)
            {
                break;
            }
        }

        return MouthShape.X;
    }
}
