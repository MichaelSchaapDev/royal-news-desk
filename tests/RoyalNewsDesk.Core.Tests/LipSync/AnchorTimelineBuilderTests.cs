using RoyalNewsDesk.Core.LipSync;

namespace RoyalNewsDesk.Core.Tests.LipSync;

public class AnchorTimelineBuilderTests
{
    [Fact]
    public void FramesSumToTotalDuration()
    {
        var mouths = new MouthCueTrack(2.0,
        [
            new MouthCue(0.0, 0.5, MouthShape.B),
            new MouthCue(0.5, 1.3, MouthShape.C),
            new MouthCue(1.3, 2.0, MouthShape.X),
        ]);
        var blinks = new[] { (0.8, 0.92) };

        var intervals = AnchorTimelineBuilder.Build(mouths, blinks, 2.0);

        Assert.Equal(50, intervals.Sum(i => i.Frames)); // 2 s at 25 fps
        Assert.All(intervals, i => Assert.True(i.Frames >= 1));
    }

    [Fact]
    public void BlinkClosesEyesMidCue()
    {
        var mouths = new MouthCueTrack(2.0, [new MouthCue(0.0, 2.0, MouthShape.C)]);
        var blinks = new[] { (1.0, 1.12) };

        var intervals = AnchorTimelineBuilder.Build(mouths, blinks, 2.0);

        Assert.Equal(3, intervals.Count);
        Assert.False(intervals[0].EyesClosed);
        Assert.True(intervals[1].EyesClosed);
        Assert.Equal(MouthShape.C, intervals[1].Mouth);
        Assert.Equal(3, intervals[1].Frames); // 0.12 s = 3 frames
        Assert.False(intervals[2].EyesClosed);
    }

    [Fact]
    public void MergesIdenticalNeighbors()
    {
        // Two X cues back to back stay one interval.
        var mouths = new MouthCueTrack(1.0,
        [
            new MouthCue(0.0, 0.5, MouthShape.X),
            new MouthCue(0.5, 1.0, MouthShape.X),
        ]);

        var intervals = AnchorTimelineBuilder.Build(mouths, [], 1.0);

        var single = Assert.Single(intervals);
        Assert.Equal(MouthShape.X, single.Mouth);
        Assert.Equal(25, single.Frames);
    }

    [Fact]
    public void WritesWellFormedFfconcat()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "anchor.ffconcat");
        var intervals = new List<AnchorInterval>
        {
            new(MouthShape.X, false, 12),
            new(MouthShape.B, false, 3),
        };

        AnchorTimelineBuilder.WriteFfconcat(path, intervals);
        var lines = File.ReadAllLines(path);

        Assert.Equal("ffconcat version 1.0", lines[0]);
        Assert.Equal("file 'X_open.png'", lines[1]);
        Assert.Equal("duration 0.480", lines[2]);
        Assert.Equal("file 'B_open.png'", lines[3]);
        Assert.Equal("duration 0.120", lines[4]);
        // Last entry repeats without a duration to survive the demuxer quirk.
        Assert.Equal("file 'B_open.png'", lines[5]);
    }

    [Fact]
    public void GapsFallBackToRest()
    {
        var mouths = new MouthCueTrack(1.0, [new MouthCue(0.0, 0.4, MouthShape.D)]);

        var intervals = AnchorTimelineBuilder.Build(mouths, [], 1.0);

        Assert.Equal(2, intervals.Count);
        Assert.Equal(MouthShape.D, intervals[0].Mouth);
        Assert.Equal(MouthShape.X, intervals[1].Mouth);
    }

    [Fact]
    public void BlinksAreDeterministic()
    {
        var first = BlinkScheduler.Schedule("Royal Week", 60);
        var second = BlinkScheduler.Schedule("Royal Week", 60);
        var other = BlinkScheduler.Schedule("Other Title", 60);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
        Assert.All(first, b => Assert.True(b.End <= 60));
        Assert.True(first.Count > 8); // roughly every 2.4 to 5.2 seconds
    }
}
