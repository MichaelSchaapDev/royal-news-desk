using RoyalNewsDesk.Core.Processes;

namespace RoyalNewsDesk.Core.Tools;

/// <summary>Result of probing one tool executable.</summary>
public sealed record ToolHealth(ExternalTool Tool, bool Ok, string? Detail);

/// <summary>Verifies each bundled tool starts and answers a version query.</summary>
public sealed class ToolHealthCheck(IToolLocator locator, IProcessRunner runner)
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(20);

    public async Task<IReadOnlyList<ToolHealth>> CheckAllAsync(CancellationToken ct)
    {
        var results = new List<ToolHealth>
        {
            await CheckAsync(ExternalTool.Ffmpeg, ["-version"], [0], ct).ConfigureAwait(false),
            await CheckAsync(ExternalTool.Ffprobe, ["-version"], [0], ct).ConfigureAwait(false),
            // Piper has no --version; --help prints usage. Some builds exit 1 for it.
            await CheckAsync(ExternalTool.Piper, ["--help"], [0, 1], ct).ConfigureAwait(false),
            await CheckAsync(ExternalTool.Rhubarb, ["--version"], [0], ct).ConfigureAwait(false),
        };
        return results;
    }

    private async Task<ToolHealth> CheckAsync(
        ExternalTool tool,
        IReadOnlyList<string> arguments,
        IReadOnlyList<int> okExitCodes,
        CancellationToken ct)
    {
        var path = locator.GetToolPath(tool);
        if (!File.Exists(path))
        {
            return new ToolHealth(tool, Ok: false, Detail: "not found: " + path);
        }

        try
        {
            var result = await runner.RunAsync(
                new ProcessSpec
                {
                    ExePath = path,
                    Arguments = arguments,
                    Timeout = ProbeTimeout,
                },
                ct).ConfigureAwait(false);

            if (result.TimedOut)
            {
                return new ToolHealth(tool, Ok: false, Detail: "probe timed out");
            }

            return okExitCodes.Contains(result.ExitCode)
                ? new ToolHealth(tool, Ok: true, Detail: null)
                : new ToolHealth(tool, Ok: false, Detail: Formatting.Inv.F($"exit {result.ExitCode}: {result.StdErrTail}"));
        }
        catch (ProcessStartException ex)
        {
            return new ToolHealth(tool, Ok: false, Detail: ex.Reason + ": " + ex.Message);
        }
    }
}
