using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RoyalNewsDesk.App.ViewModels;

public partial class AboutViewModel : ObservableObject
{
    public string VersionText { get; } =
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "0.0.0";

    public string RepositoryUrl => "https://github.com/MichaelSchaapDev/royal-news-desk";
}
