# Architecture

Two projects, one rule: `RoyalNewsDesk.App` (WPF) depends on `RoyalNewsDesk.Core`; never the other way. Core holds all pipeline logic, has no UI types, and reports progress as structured events (`StepProgress`) that the app localizes. Tests fake exactly one seam: `IProcessRunner`, the only door to external programs.

## The pipeline

`EpisodePipeline.ProduceAsync` runs ten steps in order:

1. **CheckTools**: probe ffmpeg, ffprobe, piper, rhubarb; confirm the voice model.
2. **PrepareEpisode**: free-space check, fresh work folder, parse the script into a `SpeechPlan` (soft warnings, one fatal case: no text).
3. **Voice**: Piper speaks each sentence into its own wav; `VoiceTrackAssembler` stitches them with fixed gaps and derives the `Timeline` from the same sample counts; two-pass loudnorm; `MasterAudioMixer` lays jingle, voice and sting on one 48 kHz stereo track.
4. **LipSync**: one Rhubarb pass over the voice track; on failure, synthetic visemes keep the episode alive.
5. **Graphics**: SkiaSharp renders studio rasters, lower thirds, ticker strip (seamless loop), and the intro and outro cards.
6. **Presenter**: renders through the `IPresenterEngine` seam, chosen per episode. Animated: mouth cues plus deterministic blinks become frame-exact pose runs, written as an ffconcat stills list next to 18 pose PNGs. Photoreal: the downloaded SadTalker bundle animates the portrait photo from the voice track; the clip is normalized to 25 fps h264 and later composited inside the correspondent frame. If the engine crashes, the animated anchor takes over with warning W801 and the episode still finishes.
7. **Assemble**: ffmpeg renders intro, outro and the body (one filter graph from `FiltergraphBuilder` covering the anchor, lower thirds, panels and ticker, written to a script file), then concats with stream copy and muxes the master audio with faststart.
8. **Subtitles**: SRT from the timeline; optional styled ASS burned into the body.
9. **Thumbnail**: 1280x720 PNG with the title and the first episode photo.
10. **Export**: `OutputValidator` probes the file (codecs, geometry, duration, loudness, faststart); only a passing file gets copied to the user's Videos folder.

`timeline.json` in the episode folder records every timing decision; it is the first place to look when something looks off.

## Windows rules that keep it working

- Invariant culture for every number that meets ffmpeg, ffconcat or SRT text. The target machine runs a Dutch locale where `0.5` prints as `0,5`.
- Subprocesses always get cwd = episode work dir and relative forward-slash paths.
- Generated file names are ASCII; user images are copied in under generated names.
- No ffmpeg drawtext; all text pre-renders to PNG through SkiaSharp.

## Presenter engines

Photoreal engines are not installed with the app; the settings page downloads them as SHA256-verified zip bundles into `%LOCALAPPDATA%\RoyalNewsDeskStudio\presenters\{engineId}\` (`PresenterEngineManager`, mirroring the voice downloads plus an extraction phase). A bundle is a self-contained folder: embeddable Python with pinned packages, the SadTalker source at a pinned commit, the model weights, and its own ffmpeg. `SadTalkerPresenterEngine` runs it with a confined environment and relative ASCII paths (the profile folder may contain diacritics that break C-level file APIs inside Python). Bundles are built once by `tools/build-presenter-bundle.ps1` and uploaded to the `presenter-engines-v1` release.

## Adding a paid provider later

The seams are interfaces in Core: `ITtsEngine` (swap Piper for ElevenLabs), `ILipSyncEngine`, `IPresenterEngine` (a paid avatar service slots in beside the two local engines), `IPublishTarget` (YouTube upload; interface exists, no implementation yet). Implement the interface, register it in `App.xaml.cs`, and the pipeline stays untouched.

## Disk layout

| What | Where |
|---|---|
| Install (Velopack) | `%LOCALAPPDATA%\RoyalNewsDesk\current\` by default; `Setup.exe --installto <dir>` puts it anywhere and updates follow |
| Settings, episodes, logs | `%LOCALAPPDATA%\RoyalNewsDeskStudio\` |
| Voices and presenter engines | `models\` and `presenters\` under the same root, or under `AiStorageFolder` when set; `AppPaths.AiRootOverride` applies it, `AiStorageMover` relocates existing downloads with copy-then-delete |
| Finished videos | `%USERPROFILE%\Videos\Royal News Desk\` |
