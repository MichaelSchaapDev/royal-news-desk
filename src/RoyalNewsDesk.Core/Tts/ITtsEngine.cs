namespace RoyalNewsDesk.Core.Tts;

/// <summary>One sentence to synthesize into its own wav file.</summary>
public sealed record TtsSentence(int Ordinal, string Text, string WavPath);

/// <summary>A full episode's worth of sentences, synthesized with one voice.</summary>
public sealed record TtsBatch(
    IReadOnlyList<TtsSentence> Sentences,
    string ModelPath,
    string ConfigPath,
    double LengthScale);

public sealed record TtsSentenceResult(int Ordinal, string Text, string WavPath, long Samples, int SampleRate);

/// <summary>Text to speech. V1 is Piper; a paid provider can implement this later.</summary>
public interface ITtsEngine
{
    Task<IReadOnlyList<TtsSentenceResult>> SynthesizeAsync(
        TtsBatch batch,
        IProgress<double>? progress,
        CancellationToken ct);
}
