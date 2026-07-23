using System.Globalization;
using RoyalNewsDesk.App.Resources;
using RoyalNewsDesk.Core.Models;
using RoyalNewsDesk.Core.Pipeline;

namespace RoyalNewsDesk.App.Services;

/// <summary>
/// Turns Core's structured pipeline events into the words mom reads. Core
/// stays string-free; every user-facing sentence lives in the resx files.
/// </summary>
public static class StepProgressLocalizer
{
    public static string StepText(StepProgress progress)
    {
        if (progress is { Step: PipelineStepId.Voice, State: StepState.Running, ItemIndex: { } index, ItemCount: { } count }
            && count > 0)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.Step_Voice_Progress,
                index.ToString(CultureInfo.CurrentCulture),
                count.ToString(CultureInfo.CurrentCulture));
        }

        return Strings.ByName("Step_" + progress.Step);
    }

    public static string ErrorText(PipelineErrorCode code) => Strings.ByName("PipelineError_" + code);

    public static string WarningText(PipelineWarning warning)
    {
        var template = Strings.ByName("Warning_" + warning.Code);
        return template.Contains("{0}", StringComparison.Ordinal)
            ? string.Format(CultureInfo.CurrentCulture, template, warning.Detail ?? "")
            : template;
    }
}
