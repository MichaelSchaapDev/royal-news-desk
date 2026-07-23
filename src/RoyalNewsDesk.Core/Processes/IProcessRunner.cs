namespace RoyalNewsDesk.Core.Processes;

/// <summary>
/// The single seam through which every external tool runs. Tests fake this;
/// production uses <see cref="ProcessRunner"/>.
/// </summary>
public interface IProcessRunner
{
    /// <exception cref="ProcessStartException">The executable would not start.</exception>
    /// <exception cref="OperationCanceledException">The caller's token was canceled; the process tree was killed.</exception>
    Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct);
}
