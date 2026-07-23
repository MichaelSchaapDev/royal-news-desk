using System.Buffers.Binary;

namespace RoyalNewsDesk.Core.Audio;

public sealed record WavInfo(int SampleRate, int Channels, int BitsPerSample, long DataBytes)
{
    public long Samples => DataBytes / (Channels * (BitsPerSample / 8));

    public TimeSpan Duration => TimeSpan.FromSeconds((double)Samples / SampleRate);
}

/// <summary>
/// Minimal RIFF/WAVE reader. Walks chunks instead of assuming a 44-byte
/// header, because some writers add LIST or fact chunks before the data.
/// </summary>
public static class WavFile
{
    public static WavInfo ReadInfo(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        ReadExactly(stream, header);
        if (!header[..4].SequenceEqual("RIFF"u8) || !header[8..12].SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("Not a RIFF/WAVE file: " + path);
        }

        int sampleRate = 0, channels = 0, bitsPerSample = 0;
        long dataBytes = -1;
        Span<byte> chunkHeader = stackalloc byte[8];
        Span<byte> fmt = stackalloc byte[16];

        while (stream.Position + 8 <= stream.Length)
        {
            ReadExactly(stream, chunkHeader);
            var chunkId = chunkHeader[..4];
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);

            if (chunkId.SequenceEqual("fmt "u8))
            {
                ReadExactly(stream, fmt);
                channels = BinaryPrimitives.ReadUInt16LittleEndian(fmt[2..4]);
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(fmt[4..8]);
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(fmt[14..16]);
                SkipPadded(stream, chunkSize - 16);
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                dataBytes = chunkSize;
                // 0xFFFFFFFF means "rest of file" (streamed writers).
                if (chunkSize == uint.MaxValue)
                {
                    dataBytes = stream.Length - stream.Position;
                }

                SkipPadded(stream, dataBytes);
            }
            else
            {
                SkipPadded(stream, chunkSize);
            }
        }

        if (sampleRate == 0 || channels == 0 || dataBytes < 0)
        {
            throw new InvalidDataException("Missing fmt or data chunk: " + path);
        }

        return new WavInfo(sampleRate, channels, bitsPerSample, dataBytes);
    }

    /// <summary>Reads interleaved 16-bit PCM samples of any channel count.</summary>
    public static (short[] Samples, int Channels, int SampleRate) ReadPcm16Interleaved(string path)
    {
        var info = ReadInfo(path);
        if (info.BitsPerSample != 16)
        {
            throw new InvalidDataException("Expected 16-bit PCM: " + path);
        }

        return (ReadDataChunk(path), info.Channels, info.SampleRate);
    }

    /// <summary>Reads the raw 16-bit PCM samples of a mono wav.</summary>
    public static short[] ReadMonoPcm16(string path)
    {
        var info = ReadInfo(path);
        if (info.Channels != 1 || info.BitsPerSample != 16)
        {
            throw new InvalidDataException("Expected 16-bit mono PCM: " + path);
        }

        return ReadDataChunk(path);
    }

    private static short[] ReadDataChunk(string path)
    {

        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[12];
        ReadExactly(stream, header);
        Span<byte> chunkHeader = stackalloc byte[8];
        while (true)
        {
            ReadExactly(stream, chunkHeader);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(chunkHeader[4..8]);
            if (chunkHeader[..4].SequenceEqual("data"u8))
            {
                var bytes = chunkSize == uint.MaxValue ? stream.Length - stream.Position : chunkSize;
                var samples = new short[bytes / 2];
                var buffer = new byte[bytes];
                ReadExactly(stream, buffer);
                Buffer.BlockCopy(buffer, 0, samples, 0, (int)bytes);
                return samples;
            }

            SkipPadded(stream, chunkSize);
        }
    }

    /// <summary>Writes 16-bit PCM samples as a canonical wav file.</summary>
    public static void WritePcm16(string path, int sampleRate, int channels, ReadOnlySpan<short> samples)
    {
        using var stream = File.Create(path);
        var dataBytes = samples.Length * 2;
        Span<byte> header = stackalloc byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header[4..], (uint)(36 + dataBytes));
        "WAVE"u8.CopyTo(header[8..]);
        "fmt "u8.CopyTo(header[12..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[16..], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header[20..], 1); // PCM
        BinaryPrimitives.WriteUInt16LittleEndian(header[22..], (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header[24..], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header[28..], (uint)(sampleRate * channels * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(header[32..], (ushort)(channels * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(header[34..], 16);
        "data"u8.CopyTo(header[36..]);
        BinaryPrimitives.WriteUInt32LittleEndian(header[40..], (uint)dataBytes);
        stream.Write(header);

        var bytes = new byte[dataBytes];
        System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples).CopyTo(bytes);
        stream.Write(bytes);
    }

    private static void ReadExactly(Stream stream, Span<byte> buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = stream.Read(buffer[total..]);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            total += read;
        }
    }

    private static void SkipPadded(Stream stream, long bytes)
    {
        // RIFF chunks are word-aligned.
        stream.Seek(bytes + (bytes % 2), SeekOrigin.Current);
    }
}
