namespace RoyalNewsDesk.Core.Tools;

/// <summary>An external tool started but failed to do its job.</summary>
public sealed class ToolExecutionException : Exception
{
    public ToolExecutionException(ExternalTool tool, string detail, Exception? inner = null)
        : base(tool + " failed: " + detail, inner)
    {
        Tool = tool;
        Detail = detail;
    }

    public ExternalTool Tool { get; }

    public string Detail { get; }
}
