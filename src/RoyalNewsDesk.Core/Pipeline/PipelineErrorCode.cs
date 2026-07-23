namespace RoyalNewsDesk.Core.Pipeline;

/// <summary>Stable error codes the app maps to friendly localized messages.</summary>
public enum PipelineErrorCode
{
    None,
    ToolMissing,
    ToolBlocked,
    VoiceModelMissing,
    ToolFailed,
    DiskFull,
    AccessDenied,
    ImageUnreadable,
    ScriptEmpty,
    OutputInvalid,
    Canceled,
    Unknown,
}
