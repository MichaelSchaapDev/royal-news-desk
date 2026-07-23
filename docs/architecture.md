# Architecture

Two projects, one rule: `RoyalNewsDesk.App` (WPF) depends on `RoyalNewsDesk.Core`; never the other way. Core holds all pipeline logic, has no UI types, and reports progress as structured events (`StepProgress`) that the app localizes. Tests fake exactly one seam: `IProcessRunner`, the only door to external programs.

## The pipeline

`EpisodePipeline.ProduceAsync` runs ten steps in order:

1. **CheckTools**: probe ffmpeg, ffprobe, piper, rhubarb; confirm the voice model.
2. **PrepareEpisode**: free-space check, fresh work folder, parse the script into a `SpeechPlan` (soft warnings, one fatal case: no text).
3. **Voice**: Piper speaks each sentence into its own wav; `VoiceTrackAssembler` stitches them with fixed gaps and derives the `Timeline` from the same sample counts; two-pass loudnorm; `MasterAudioMixer` lays jingle, voice and sting on one 48 kHz stereo track.
4. **LipSync**: one Rhubarb pass over the voice track; on failure, synthetic visemes keep the episode alive.
5. **Graphics**: SkiaSharp renders studio rasters, lower thirds, ticker strip (seamless loop), desk plate, cards and the logo bug.
6. **AnchorAnimation**: mouth cues plus deterministic blinks become frame-exact pose runs, written as an ffconcat stills list next to 18 pose PNGs.
7. **Assemble**: ffmpeg renders intro, outro and the body (one filter graph from `FiltergraphBuilder`, written to a script file), then concats with stream copy and muxes the master audio with faststart.
8. **Subtitles**: SRT from the timeline; optional styled ASS burned into the body.
9. **Thumbnail**: 1280x720 PNG with the title and the first episode photo.
10. **Export**: `OutputValidator` probes the file (codecs, geometry, duration, loudness, faststart); only a passing file gets copied to the user's Videos folder.

`timeline.json` in the episode folder records every timing decision; it is the first place to look when something looks off.

## Windows rules that keep it working

- Invariant culture for every number that meets ffmpeg, ffconcat or SRT text. The target machine runs a Dutch locale where `0.5` prints as `0,5`.
- Subprocesses always get cwd = episode work dir and relative forward-slash paths.
- Generated file names are ASCII; user images are copied in under generated names.
- No ffmpeg drawtext; all text pre-renders to PNG through SkiaSharp.

## Adding a paid provider later

The seams are interfaces in Core: `ITtsEngine` (swap Piper for ElevenLabs), `ILipSyncEngine` and the anchor rendering (swap for HeyGen-style video), `IPublishTarget` (YouTube upload; interface exists, no implementation yet). Implement the interface, register it in `App.xaml.cs`, and the pipeline stays untouched.

## Disk layout

| What | Where |
|---|---|
| Install (Velopack) | `%LOCALAPPDATA%\RoyalNewsDesk\current\` |
| Settings, voices, episodes, logs | `%LOCALAPPDATA%\RoyalNewsDeskStudio\` |
| Finished videos | `%USERPROFILE%\Videos\Royal News Desk\` |
