namespace RoyalNewsDesk.Core.Processes;

/// <summary>A subprocess invocation. Arguments stay a list; they are never joined into a string.</summary>
public sealed record ProcessSpec
{
    public required string ExePath { get; init; }

    public IReadOnlyList<string> Arguments { get; init; } = [];

    public string? WorkingDirectory { get; init; }

    /// <summary>Written to stdin as UTF-8 without BOM, then stdin closes.</summary>
    public string? StdinText { get; init; }

    /// <summary>Watchdog. When it expires the process tree is killed and the result reports TimedOut.</summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Environment variables applied over the inherited environment. A null
    /// value removes the variable. Setting PATH here replaces PATH outright.
    /// </summary>
    public IReadOnlyDictionary<string, string?>? EnvironmentOverrides { get; init; }

    /// <summary>Called for every stdout line (used to parse ffmpeg -progress).</summary>
    public Action<string>? OnOutputLine { get; init; }

    /// <summary>Called for every stderr line (used to parse rhubarb progress).</summary>
    public Action<string>? OnErrorLine { get; init; }
}
