namespace RoyalNewsDesk.Core.Tools;

/// <summary>
/// Finds tools under the app's own install folder: {app}\tools\{tool}\{tool}.exe.
/// The ROYALNEWSDESK_TOOLS_DIR environment variable overrides the base folder
/// for development and tests.
/// </summary>
public sealed class InstalledToolLocator : IToolLocator
{
    private readonly string _baseDir;

    public InstalledToolLocator()
        : this(Environment.GetEnvironmentVariable("ROYALNEWSDESK_TOOLS_DIR")
               ?? Path.Combine(AppContext.BaseDirectory, "tools"))
    {
    }

    public InstalledToolLocator(string baseDir)
    {
        _baseDir = baseDir;
    }

    public string GetToolPath(ExternalTool tool) => tool switch
    {
        ExternalTool.Ffmpeg => Path.Combine(_baseDir, "ffmpeg", "ffmpeg.exe"),
        ExternalTool.Ffprobe => Path.Combine(_baseDir, "ffmpeg", "ffprobe.exe"),
        ExternalTool.Piper => Path.Combine(_baseDir, "piper", "piper.exe"),
        ExternalTool.Rhubarb => Path.Combine(_baseDir, "rhubarb", "rhubarb.exe"),
        _ => throw new ArgumentOutOfRangeException(nameof(tool), tool, null),
    };
}
