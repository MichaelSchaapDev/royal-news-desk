namespace RoyalNewsDesk.Core.Pipeline;

/// <summary>
/// One progress event from the pipeline. Structured on purpose: the app turns
/// (Step, State, Error) into localized text; Core stays string-free except for
/// technical diagnostics.
/// </summary>
/// <param name="Step">Which step this event is about.</param>
/// <param name="State">The step's new state.</param>
/// <param name="Fraction">0..1 progress within the step, when known.</param>
/// <param name="ItemIndex">1-based item counter (for example, sentence 12 of 48).</param>
/// <param name="ItemCount">Total items for <paramref name="ItemIndex"/>.</param>
/// <param name="Error">Why the step failed, when State is Failed.</param>
/// <param name="TechnicalDetail">
/// Exit codes and stderr tails for the details expander and the log. Never
/// shown as the main message and never localized.
/// </param>
public sealed record StepProgress(
    PipelineStepId Step,
    StepState State,
    double? Fraction = null,
    int? ItemIndex = null,
    int? ItemCount = null,
    PipelineErrorCode Error = PipelineErrorCode.None,
    string? TechnicalDetail = null);
