namespace RoyalNewsDesk.Core.VoiceModels;

public enum VoiceDownloadFailure
{
    Network,
    ChecksumMismatch,
    Disk,
}

public sealed class VoiceDownloadException : Exception
{
    public VoiceDownloadException(VoiceDownloadFailure reason, string message, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    public VoiceDownloadFailure Reason { get; }
}
