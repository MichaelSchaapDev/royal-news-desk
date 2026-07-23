using System.Diagnostics;
using RoyalNewsDesk.Core.Processes;

namespace RoyalNewsDesk.Core.Tests.Processes;

/// <summary>
/// Integration tests against real OS executables (cmd, ping, findstr). They
/// exist because the runner's pipe handling and kill behavior cannot be
/// faked meaningfully.
/// </summary>
public class ProcessRunnerTests
{
    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    private static string Ping => Path.Combine(Environment.SystemDirectory, "ping.exe");

    private static string FindStr => Path.Combine(Environment.SystemDirectory, "findstr.exe");

    [Fact]
    public async Task CapturesStdout()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(
            new ProcessSpec { ExePath = Cmd, Arguments = ["/c", "echo hallo wereld"] },
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Success);
        Assert.Contains("hallo wereld", result.StdOutTail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CapturesStderr()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(
            new ProcessSpec { ExePath = Cmd, Arguments = ["/c", "echo oeps 1>&2"] },
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("oeps", result.StdErrTail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsExitCode()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(
            new ProcessSpec { ExePath = Cmd, Arguments = ["/c", "exit 3"] },
            CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task WritesStdin()
    {
        var runner = new ProcessRunner();
        var result = await runner.RunAsync(
            new ProcessSpec
            {
                ExePath = FindStr,
                Arguments = ["koninklijk"],
                StdinText = "koninklijk nieuws\r\nander nieuws\r\n",
            },
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("koninklijk nieuws", result.StdOutTail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvokesLineCallbacks()
    {
        var lines = new List<string>();
        var runner = new ProcessRunner();
        await runner.RunAsync(
            new ProcessSpec
            {
                ExePath = Cmd,
                Arguments = ["/c", "echo een&echo twee"],
                OnOutputLine = lines.Add,
            },
            CancellationToken.None);

        Assert.Contains("een", lines);
        Assert.Contains("twee", lines);
    }

    [Fact]
    public async Task CancelKillsTheProcess()
    {
        var runner = new ProcessRunner();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(
            new ProcessSpec { ExePath = Ping, Arguments = ["-n", "30", "127.0.0.1"] },
            cts.Token));

        stopwatch.Stop();
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            "cancellation should kill ping quickly, took " + stopwatch.Elapsed.ToString());
    }

    [Fact]
    public async Task TimeoutKillsAndReports()
    {
        var runner = new ProcessRunner();
        var stopwatch = Stopwatch.StartNew();

        var result = await runner.RunAsync(
            new ProcessSpec
            {
                ExePath = Ping,
                Arguments = ["-n", "30", "127.0.0.1"],
                Timeout = TimeSpan.FromMilliseconds(400),
            },
            CancellationToken.None);

        stopwatch.Stop();
        Assert.True(result.TimedOut);
        Assert.False(result.Success);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            "timeout should kill ping quickly, took " + stopwatch.Elapsed.ToString());
    }

    [Fact]
    public async Task MissingExecutableThrowsTypedException()
    {
        var runner = new ProcessRunner();
        var ex = await Assert.ThrowsAsync<ProcessStartException>(() => runner.RunAsync(
            new ProcessSpec { ExePath = @"C:\does\not\exist\nope.exe" },
            CancellationToken.None));

        Assert.Equal(ProcessStartFailure.FileNotFound, ex.Reason);
    }
}
