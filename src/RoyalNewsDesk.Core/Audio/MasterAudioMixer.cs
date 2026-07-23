using RoyalNewsDesk.Core.Video;

namespace RoyalNewsDesk.Core.Audio;

/// <summary>
/// Builds the one continuous 48 kHz stereo master track: intro jingle,
/// normalized voice, outro sting. One track means no AAC priming gaps at
/// the part joins.
/// </summary>
public static class MasterAudioMixer
{
    private const int Rate = 48000;

    public static void Mix(
        Timeline timeline,
        string voiceNorm48kMonoPath,
        string introJinglePath,
        string outroStingPath,
        string outputPath)
    {
        var totalSamples = (long)Math.Ceiling(timeline.TotalDuration * Rate);
        var left = new int[totalSamples];
        var right = new int[totalSamples];

        // Voice: mono to dual mono at body start.
        var voice = WavFile.ReadMonoPcm16(voiceNorm48kMonoPath);
        AddMono(left, right, voice, (long)(timeline.BodyStart * Rate));

        // Intro jingle from t=0, faded out over its final second so it hands
        // over cleanly to the first sentence.
        var (jingle, jingleChannels, jingleRate) = WavFile.ReadPcm16Interleaved(introJinglePath);
        AddStereoFaded(
            left,
            right,
            jingle,
            jingleChannels,
            jingleRate,
            offsetSamples: 0,
            maxSeconds: timeline.IntroDuration + 1.0,
            fadeOutSeconds: 1.0,
            fadeInSeconds: 0);

        // Outro sting at the outro start, faded in briefly.
        var (sting, stingChannels, stingRate) = WavFile.ReadPcm16Interleaved(outroStingPath);
        AddStereoFaded(
            left,
            right,
            sting,
            stingChannels,
            stingRate,
            offsetSamples: (long)(timeline.OutroStart * Rate),
            maxSeconds: timeline.OutroDuration - 0.5,
            fadeOutSeconds: 0.8,
            fadeInSeconds: 0.3);

        var interleaved = new short[totalSamples * 2];
        for (long i = 0; i < totalSamples; i++)
        {
            interleaved[i * 2] = Clamp(left[i]);
            interleaved[i * 2 + 1] = Clamp(right[i]);
        }

        WavFile.WritePcm16(outputPath, Rate, 2, interleaved);
    }

    private static void AddMono(int[] left, int[] right, short[] mono, long offset)
    {
        for (long i = 0; i < mono.Length && offset + i < left.Length; i++)
        {
            left[offset + i] += mono[i];
            right[offset + i] += mono[i];
        }
    }

    private static void AddStereoFaded(
        int[] left,
        int[] right,
        short[] interleaved,
        int channels,
        int sourceRate,
        long offsetSamples,
        double maxSeconds,
        double fadeOutSeconds,
        double fadeInSeconds)
    {
        if (sourceRate != Rate)
        {
            // Assets ship at 48 kHz; anything else is a build mistake.
            throw new InvalidDataException("Expected 48 kHz audio asset, got " + sourceRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var frames = interleaved.Length / channels;
        var maxFrames = (long)(maxSeconds * Rate);
        var usable = Math.Min(frames, maxFrames);
        var fadeOutFrames = (long)(fadeOutSeconds * Rate);
        var fadeInFrames = (long)(fadeInSeconds * Rate);

        for (long i = 0; i < usable && offsetSamples + i < left.Length; i++)
        {
            var gain = 1.0;
            if (fadeInFrames > 0 && i < fadeInFrames)
            {
                gain *= i / (double)fadeInFrames;
            }

            var remaining = usable - i;
            if (fadeOutFrames > 0 && remaining < fadeOutFrames)
            {
                gain *= remaining / (double)fadeOutFrames;
            }

            var l = interleaved[i * channels];
            var r = channels > 1 ? interleaved[i * channels + 1] : l;
            left[offsetSamples + i] += (int)(l * gain);
            right[offsetSamples + i] += (int)(r * gain);
        }
    }

    private static short Clamp(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);
}
