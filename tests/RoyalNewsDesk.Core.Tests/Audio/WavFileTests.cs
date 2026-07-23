using RoyalNewsDesk.Core.Audio;

namespace RoyalNewsDesk.Core.Tests.Audio;

public class WavFileTests
{
    [Fact]
    public void RoundTripsPcm16()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "test.wav");
        var samples = new short[22050]; // one second of silence at 22050 Hz
        samples[100] = 12345;

        WavFile.WritePcm16(path, 22050, 1, samples);
        var info = WavFile.ReadInfo(path);

        Assert.Equal(22050, info.SampleRate);
        Assert.Equal(1, info.Channels);
        Assert.Equal(16, info.BitsPerSample);
        Assert.Equal(22050, info.Samples);
        Assert.Equal(1.0, info.Duration.TotalSeconds, 3);

        var read = WavFile.ReadMonoPcm16(path);
        Assert.Equal(12345, read[100]);
        Assert.Equal(samples.Length, read.Length);
    }

    [Fact]
    public void ReadsFilesWithExtraChunksBeforeData()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "extra.wav");

        // Handcraft: RIFF header, fmt, a LIST chunk, then data.
        using (var stream = File.Create(path))
        using (var writer = new BinaryWriter(stream))
        {
            var data = new byte[400];
            writer.Write("RIFF"u8);
            writer.Write((uint)(4 + 24 + 12 + 8 + data.Length));
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16u);
            writer.Write((ushort)1);
            writer.Write((ushort)1);
            writer.Write(22050u);
            writer.Write(44100u);
            writer.Write((ushort)2);
            writer.Write((ushort)16);
            writer.Write("LIST"u8);
            writer.Write(4u);
            writer.Write("INFO"u8);
            writer.Write("data"u8);
            writer.Write((uint)data.Length);
            writer.Write(data);
        }

        var info = WavFile.ReadInfo(path);
        Assert.Equal(200, info.Samples);
    }

    [Fact]
    public void RejectsNonWavFiles()
    {
        using var temp = new TempDir();
        var path = Path.Combine(temp.Path, "not.wav");
        File.WriteAllText(path, "certainly not audio data at all");

        Assert.Throws<InvalidDataException>(() => WavFile.ReadInfo(path));
    }
}
