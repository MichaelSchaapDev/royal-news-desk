namespace RoyalNewsDesk.Core.Tools;

public enum ExternalTool
{
    Ffmpeg,
    Ffprobe,
    Piper,
    Rhubarb,
}

/// <summary>Resolves where the bundled tool executables live.</summary>
public interface IToolLocator
{
    /// <summary>Full path for the tool. The file may not exist; the health check verifies that.</summary>
    string GetToolPath(ExternalTool tool);
}
