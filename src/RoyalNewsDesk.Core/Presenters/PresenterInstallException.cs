namespace RoyalNewsDesk.Core.Presenters;

public enum PresenterInstallFailure
{
    Network,
    ChecksumMismatch,
    Disk,
    Extraction,
}

public sealed class PresenterInstallException : Exception
{
    public PresenterInstallException(PresenterInstallFailure reason, string message, Exception? inner = null)
        : base(message, inner)
    {
        Reason = reason;
    }

    public PresenterInstallFailure Reason { get; }
}
