# Third-party notices

Royal News Desk Studio bundles or downloads the following third-party software and assets. Each keeps its own license.

## Bundled programs (in the installer, run as separate processes)

- **FFmpeg** (ffmpeg.exe, ffprobe.exe): GPL build by BtbN, from https://github.com/BtbN/FFmpeg-Builds (pinned in `tools/tools.lock.json`). FFmpeg is licensed under the GPL v3 for these builds; source code is available from the same page. The app invokes it as a separate program.
- **Piper** (piper.exe and libraries): MIT license, from https://github.com/rhasspy/piper, release 2023.11.14-2. Ships with espeak-ng data (GPL v3) from the same release.
- **Rhubarb Lip Sync** (rhubarb.exe and resources): MIT license, from https://github.com/DanielSWolf/rhubarb-lip-sync, release 1.14.0. Bundles PocketSphinx (BSD).

## Downloaded on demand: photoreal presenter engines

The optional SadTalker engine bundles (`sadtalker-cpu`, `sadtalker-cuda`) are
built by `tools/build-presenter-bundle.ps1`, published as release assets, and
downloaded with SHA256 verification when chosen in Settings. Each bundle
carries its own notices file (SadTalker Apache-2.0 with its ethical-use note,
Python PSF, PyTorch BSD-3 plus NVIDIA CUDA redistributables in the cuda
variant, GFPGAN Apache-2.0, facexlib MIT, libsndfile LGPL-2.1, a BtbN GPL
ffmpeg build, and the rest of the bundled packages per their dist-info
licenses).

## Downloaded on first run (not redistributed by this project)

- **Piper voice models** from https://huggingface.co/rhasspy/piper-voices (v1.0.0), verified by SHA256:
  - `en_GB-cori-high`: dataset from LibriVox, public domain.
  - `en_GB-alba-medium`: dataset from the University of Edinburgh, CC BY 4.0.
  - `en_GB-northern_english_male-medium`: dataset OpenSLR 83, CC BY-SA 4.0.

## Bundled fonts

- **IBM Plex Serif** and **IBM Plex Sans**: SIL Open Font License 1.1, from https://github.com/IBM/plex. The license text ships in `assets/fonts/OFL-IBM-Plex.txt`.

## Bundled audio

- The intro jingle and outro sting were synthesized for this project with FFmpeg (pure sine tones) and carry the project's MIT license.

## NuGet packages

- WPF-UI (MIT), CommunityToolkit.Mvvm (MIT), SkiaSharp and Svg.Skia (MIT), Velopack (MIT), Serilog (Apache-2.0), Microsoft.Extensions.* (MIT).
