using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace RoyalNewsDesk.Core.Processes;

/// <summary>
/// Runs subprocesses safely on Windows: arguments as a list (no string
/// joining), stdout and stderr drained concurrently (ffmpeg fills stderr and
/// deadlocks otherwise), UTF-8 pipes, watchdog timeout, and a full
/// process-tree kill on cancel.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private const int MaxTailLines = 200;

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = spec.ExePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = spec.StdinText is not null,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (spec.StdinText is not null)
        {
            startInfo.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        if (spec.WorkingDirectory is not null)
        {
            startInfo.WorkingDirectory = spec.WorkingDirectory;
        }

        foreach (var argument in spec.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process();
        process.StartInfo = startInfo;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!process.Start())
            {
                throw new ProcessStartException(
                    spec.ExePath,
                    ProcessStartFailure.Other,
                    new InvalidOperationException("Process.Start returned false."));
            }
        }
        catch (Win32Exception ex)
        {
            throw new ProcessStartException(spec.ExePath, ClassifyStartFailure(ex), ex);
        }

        var stdoutTail = new TailBuffer(MaxTailLines);
        var stderrTail = new TailBuffer(MaxTailLines);
        var stdoutTask = PumpAsync(process.StandardOutput, stdoutTail, spec.OnOutputLine);
        var stderrTask = PumpAsync(process.StandardError, stderrTail, spec.OnErrorLine);

        if (spec.StdinText is not null)
        {
            try
            {
                await process.StandardInput.WriteAsync(spec.StdinText).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // The process exited before reading everything; its exit code tells the story.
            }

            process.StandardInput.Close();
        }

        using var timeoutSource = spec.Timeout is { } timeout ? new CancellationTokenSource(timeout) : null;
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            ct,
            timeoutSource?.Token ?? CancellationToken.None);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);
            timedOut = !ct.IsCancellationRequested;
        }

        // After exit or kill the pipes hit end-of-file, so the pumps finish.
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        if (!process.HasExited)
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        stopwatch.Stop();
        ct.ThrowIfCancellationRequested();

        return new ProcessResult(
            timedOut ? -1 : process.ExitCode,
            stdoutTail.ToText(),
            stderrTail.ToText(),
            stopwatch.Elapsed,
            timedOut);
    }

    private static async Task PumpAsync(StreamReader reader, TailBuffer tail, Action<string>? onLine)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            tail.Add(line);
            onLine?.Invoke(line);
        }
    }

    private static ProcessStartFailure ClassifyStartFailure(Win32Exception ex) => ex.NativeErrorCode switch
    {
        2 or 3 => ProcessStartFailure.FileNotFound,
        5 => ProcessStartFailure.AccessDenied,
        225 => ProcessStartFailure.VirusBlocked,
        _ => ProcessStartFailure.Other,
    };

    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (Win32Exception)
        {
            // Exiting right now; nothing left to kill.
        }
    }

    /// <summary>Bounded line buffer. Written by exactly one pump task, read after the pump completes.</summary>
    private sealed class TailBuffer(int maxLines)
    {
        private readonly Queue<string> _lines = new();

        public void Add(string line)
        {
            _lines.Enqueue(line);
            if (_lines.Count > maxLines)
            {
                _lines.Dequeue();
            }
        }

        public string ToText() => string.Join(Environment.NewLine, _lines);
    }
}
