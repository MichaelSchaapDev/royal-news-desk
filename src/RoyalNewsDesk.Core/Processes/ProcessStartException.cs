namespace RoyalNewsDesk.Core.Processes;

public enum ProcessStartFailure
{
    FileNotFound,
    AccessDenied,

    /// <summary>Antivirus blocked the executable (Win32 error 225).</summary>
    VirusBlocked,
    Other,
}

/// <summary>The OS refused to start an executable.</summary>
public sealed class ProcessStartException : Exception
{
    public ProcessStartException(string exePath, ProcessStartFailure reason, Exception inner)
        : base($"Could not start '{exePath}': {reason}", inner)
    {
        ExePath = exePath;
        Reason = reason;
    }

    public string ExePath { get; }

    public ProcessStartFailure Reason { get; }
}
