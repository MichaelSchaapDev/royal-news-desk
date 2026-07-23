namespace RoyalNewsDesk.Core.Processes;

/// <summary>Outcome of a finished (or killed) subprocess.</summary>
public sealed record ProcessResult(
    int ExitCode,
    string StdOutTail,
    string StdErrTail,
    TimeSpan Elapsed,
    bool TimedOut = false)
{
    public bool Success => ExitCode == 0 && !TimedOut;
}
