namespace RoyalNewsDesk.Core.Pipeline;

/// <summary>Fatal pipeline failure, carrying the code the app localizes.</summary>
public sealed class PipelineException : Exception
{
    public PipelineException(PipelineStepId step, PipelineErrorCode code, string technicalDetail, Exception? inner = null)
        : base($"Pipeline step {step} failed with {code}: {technicalDetail}", inner)
    {
        Step = step;
        Code = code;
        TechnicalDetail = technicalDetail;
    }

    public PipelineStepId Step { get; }

    public PipelineErrorCode Code { get; }

    public string TechnicalDetail { get; }
}
