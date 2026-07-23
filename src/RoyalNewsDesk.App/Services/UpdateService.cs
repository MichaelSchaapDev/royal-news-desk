using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace RoyalNewsDesk.App.Services;

/// <summary>
/// Checks GitHub Releases for a newer version, downloads it in the
/// background, and applies it when the user chooses to restart. Does nothing
/// when the app runs unpackaged (development).
/// </summary>
public sealed class UpdateService(ILogger<UpdateService> log)
{
    private const string RepoUrl = "https://github.com/MichaelSchaapDev/royal-news-desk";

    private UpdateManager? _manager;
    private UpdateInfo? _staged;

    public async Task CheckAndStageAsync(Action onUpdateReady)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, accessToken: null, prerelease: false));
            if (!manager.IsInstalled)
            {
                return;
            }

            var info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(info).ConfigureAwait(false);
            _manager = manager;
            _staged = info;
            log.LogInformation("Update staged: {Version}", info.TargetFullRelease.Version);
            onUpdateReady();
        }
        catch (Exception ex)
        {
            // Updates are a convenience; never let them hurt the app.
            log.LogWarning(ex, "Update check failed");
        }
    }

    public void ApplyAndRestart()
    {
        if (_manager is not null && _staged is not null)
        {
            _manager.ApplyUpdatesAndRestart(_staged);
        }
    }
}
