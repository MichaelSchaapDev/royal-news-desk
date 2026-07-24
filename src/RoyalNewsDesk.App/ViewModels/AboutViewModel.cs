using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RoyalNewsDesk.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string VersionText { get; } = BuildVersionText();

    public string RepositoryUrl => "https://github.com/MichaelSchaapDev/royal-news-desk";

    public string GuideUrl => "https://github.com/MichaelSchaapDev/royal-news-desk/blob/main/docs/handleiding-nl.md";

    private static string BuildVersionText()
    {
        var info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "0.0.0";
        var plus = info.IndexOf('+', StringComparison.Ordinal);
        return plus > 0 ? info[..plus] : info;
    }
}
