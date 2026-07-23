# Royal News Desk Studio

Turn a written news script into a finished YouTube video. An AI voice reads the script, an animated news anchor presents it with lip-sync, and the app adds studio graphics, headlines, subtitles, an intro and an outro. Built for the Royal News Desk channel.

Runs on Windows. Free. No accounts, no API keys.

The first release (v0.1.0) will add a download link and a user guide here.

## Development

Requires the .NET 10 SDK.

```powershell
dotnet build
dotnet test
dotnet run --project src/RoyalNewsDesk.App
```

## License

The code is MIT. Bundled tools (FFmpeg, Piper, Rhubarb Lip Sync) keep their own licenses; see THIRD-PARTY-NOTICES.md once the first release lands.
