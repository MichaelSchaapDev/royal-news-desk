using System.Globalization;
using System.Resources;

namespace RoyalNewsDesk.App.Resources;

/// <summary>
/// Hand-written accessor over Strings.resx / Strings.nl.resx. Kept by hand
/// because the build-time resx generator does not survive WPF's two-pass
/// XAML compilation. Adding a string means: add it to both resx files and
/// add one property here.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Rm =
        new("RoyalNewsDesk.App.Resources.Strings", typeof(Strings).Assembly);

    private static string Get(string key) =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Dynamic lookup for generated key families (Step_*, PipelineError_*, Warning_*).</summary>
    public static string ByName(string key) => Get(key);

    public static string Produce_Header => Get(nameof(Produce_Header));

    public static string Produce_Cancel => Get(nameof(Produce_Cancel));

    public static string Produce_DoneHeader => Get(nameof(Produce_DoneHeader));

    public static string Produce_OpenFolder => Get(nameof(Produce_OpenFolder));

    public static string Produce_Again => Get(nameof(Produce_Again));

    public static string Produce_FailedHeader => Get(nameof(Produce_FailedHeader));

    public static string Produce_Retry => Get(nameof(Produce_Retry));

    public static string Produce_Warnings => Get(nameof(Produce_Warnings));

    public static string Step_Voice_Progress => Get(nameof(Step_Voice_Progress));

    public static string Produce_DurationFormat => Get(nameof(Produce_DurationFormat));

    public static string Update_Restart => Get(nameof(Update_Restart));

    public static string Editor_PresenterStyle => Get(nameof(Editor_PresenterStyle));

    public static string Editor_PresenterAnimated => Get(nameof(Editor_PresenterAnimated));

    public static string Editor_PresenterPhotoreal => Get(nameof(Editor_PresenterPhotoreal));

    public static string Editor_PhotorealNotReadyHint => Get(nameof(Editor_PhotorealNotReadyHint));

    public static string Editor_OpenSettings => Get(nameof(Editor_OpenSettings));

    public static string Settings_Presenter => Get(nameof(Settings_Presenter));

    public static string Settings_PresenterIntro => Get(nameof(Settings_PresenterIntro));

    public static string Settings_EngineExtracting => Get(nameof(Settings_EngineExtracting));

    public static string Settings_EngineNeedsGpu => Get(nameof(Settings_EngineNeedsGpu));

    public static string Settings_Portrait => Get(nameof(Settings_Portrait));

    public static string Settings_PortraitChoose => Get(nameof(Settings_PortraitChoose));

    public static string Settings_PortraitRemove => Get(nameof(Settings_PortraitRemove));

    public static string Settings_PortraitNone => Get(nameof(Settings_PortraitNone));

    public static string Settings_PortraitConsentHint => Get(nameof(Settings_PortraitConsentHint));

    public static string App_Title => Get(nameof(App_Title));

    public static string Brand_Name => Get(nameof(Brand_Name));

    public static string Brand_Studio => Get(nameof(Brand_Studio));

    public static string Nav_Episodes => Get(nameof(Nav_Episodes));

    public static string Nav_Settings => Get(nameof(Nav_Settings));

    public static string Nav_About => Get(nameof(Nav_About));

    public static string Episodes_Header => Get(nameof(Episodes_Header));

    public static string Episodes_New => Get(nameof(Episodes_New));

    public static string Episodes_Empty => Get(nameof(Episodes_Empty));

    public static string Episodes_EmptyTitle => Get(nameof(Episodes_EmptyTitle));

    public static string Episodes_Kicker => Get(nameof(Episodes_Kicker));

    public static string Produce_Kicker => Get(nameof(Produce_Kicker));

    public static string About_Links => Get(nameof(About_Links));

    public static string About_Guide => Get(nameof(About_Guide));

    public static string Episodes_HasOutput => Get(nameof(Episodes_HasOutput));

    public static string Episodes_NoOutput => Get(nameof(Episodes_NoOutput));

    public static string Episodes_Open => Get(nameof(Episodes_Open));

    public static string Episodes_Untitled => Get(nameof(Episodes_Untitled));

    public static string Episodes_DeleteConfirmTitle => Get(nameof(Episodes_DeleteConfirmTitle));

    public static string Episodes_DeleteConfirmText => Get(nameof(Episodes_DeleteConfirmText));

    public static string Common_Cancel => Get(nameof(Common_Cancel));

    public static string Common_Delete => Get(nameof(Common_Delete));

    public static string Common_Save => Get(nameof(Common_Save));

    public static string Common_Back => Get(nameof(Common_Back));

    public static string Editor_TitleLabel => Get(nameof(Editor_TitleLabel));

    public static string Editor_TitlePlaceholder => Get(nameof(Editor_TitlePlaceholder));

    public static string Editor_PasteLabel => Get(nameof(Editor_PasteLabel));

    public static string Editor_Import => Get(nameof(Editor_Import));

    public static string Editor_Segments => Get(nameof(Editor_Segments));

    public static string Editor_AddSegment => Get(nameof(Editor_AddSegment));

    public static string Editor_HeadlineLabel => Get(nameof(Editor_HeadlineLabel));

    public static string Editor_BodyLabel => Get(nameof(Editor_BodyLabel));

    public static string Editor_PickImage => Get(nameof(Editor_PickImage));

    public static string Editor_RemoveImage => Get(nameof(Editor_RemoveImage));

    public static string Editor_Produce => Get(nameof(Editor_Produce));

    public static string Editor_LoadExample => Get(nameof(Editor_LoadExample));

    public static string Editor_ImportConfirmTitle => Get(nameof(Editor_ImportConfirmTitle));

    public static string Editor_StoryN => Get(nameof(Editor_StoryN));

    public static string Editor_ScriptHint => Get(nameof(Editor_ScriptHint));

    public static string Editor_ImportConfirmText => Get(nameof(Editor_ImportConfirmText));

    public static string Common_Continue => Get(nameof(Common_Continue));

    public static string Settings_Header => Get(nameof(Settings_Header));

    public static string Settings_Language => Get(nameof(Settings_Language));

    public static string Settings_RestartHint => Get(nameof(Settings_RestartHint));

    public static string Settings_RestartNow => Get(nameof(Settings_RestartNow));

    public static string Settings_Theme => Get(nameof(Settings_Theme));

    public static string Settings_ThemeLight => Get(nameof(Settings_ThemeLight));

    public static string Settings_ThemeDark => Get(nameof(Settings_ThemeDark));

    public static string Settings_Voice => Get(nameof(Settings_Voice));

    public static string Settings_ReadingSpeed => Get(nameof(Settings_ReadingSpeed));

    public static string Settings_ReadingSpeedHint => Get(nameof(Settings_ReadingSpeedHint));

    public static string Settings_Branding => Get(nameof(Settings_Branding));

    public static string Settings_ChannelName => Get(nameof(Settings_ChannelName));

    public static string Settings_Tagline => Get(nameof(Settings_Tagline));

    public static string Settings_PrimaryColor => Get(nameof(Settings_PrimaryColor));

    public static string Settings_AccentColor => Get(nameof(Settings_AccentColor));

    public static string Settings_OutputFolder => Get(nameof(Settings_OutputFolder));

    public static string Settings_Browse => Get(nameof(Settings_Browse));

    public static string Settings_Production => Get(nameof(Settings_Production));

    public static string Settings_KeepWorkFiles => Get(nameof(Settings_KeepWorkFiles));

    public static string Settings_BurnInSubtitles => Get(nameof(Settings_BurnInSubtitles));

    public static string Settings_StudioAmbience => Get(nameof(Settings_StudioAmbience));

    public static string Settings_HigherQuality => Get(nameof(Settings_HigherQuality));

    public static string Settings_Saved => Get(nameof(Settings_Saved));

    public static string About_Header => Get(nameof(About_Header));

    public static string About_Version => Get(nameof(About_Version));

    public static string About_MadeFor => Get(nameof(About_MadeFor));

    public static string Error_Title => Get(nameof(Error_Title));

    public static string Error_Body => Get(nameof(Error_Body));

    public static string FirstRun_Title => Get(nameof(FirstRun_Title));

    public static string FirstRun_Body => Get(nameof(FirstRun_Body));

    public static string FirstRun_SizeFormat => Get(nameof(FirstRun_SizeFormat));

    public static string FirstRun_Download => Get(nameof(FirstRun_Download));

    public static string FirstRun_DownloadingFormat => Get(nameof(FirstRun_DownloadingFormat));

    public static string FirstRun_Done => Get(nameof(FirstRun_Done));

    public static string FirstRun_Continue => Get(nameof(FirstRun_Continue));

    public static string FirstRun_Failed => Get(nameof(FirstRun_Failed));

    public static string FirstRun_Retry => Get(nameof(FirstRun_Retry));

    public static string FirstRun_ToolsProblem => Get(nameof(FirstRun_ToolsProblem));

    public static string Settings_VoiceInstalled => Get(nameof(Settings_VoiceInstalled));

    public static string Settings_VoiceNotInstalled => Get(nameof(Settings_VoiceNotInstalled));

    public static string Settings_VoiceDownload => Get(nameof(Settings_VoiceDownload));

    public static string Settings_VoiceDelete => Get(nameof(Settings_VoiceDelete));
}
