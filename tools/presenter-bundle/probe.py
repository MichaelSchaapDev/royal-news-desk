# Health probe for a SadTalker presenter bundle. Prints one JSON line and
# exits 0 when everything needed for a render is present and importable.
import json
import os
import subprocess
import sys


def main() -> int:
    report = {"ok": False}
    try:
        import torch
        import torchvision  # noqa: F401
        import cv2  # noqa: F401
        import numpy  # noqa: F401
        import scipy  # noqa: F401
        import librosa  # noqa: F401
        import kornia  # noqa: F401
        import safetensors  # noqa: F401
        import facexlib  # noqa: F401
        import imageio_ffmpeg  # noqa: F401

        report["python"] = sys.version.split()[0]
        report["torch"] = torch.__version__
        report["cudaAvailable"] = bool(torch.cuda.is_available())
        if report["cudaAvailable"]:
            report["gpu"] = torch.cuda.get_device_name(0)

        engine_dir = os.path.dirname(os.path.abspath(__file__))
        expected = []
        for name in os.listdir(os.path.join(engine_dir, "checkpoints")):
            expected.append(os.path.join("checkpoints", name))
        missing = [p for p in expected if os.path.getsize(os.path.join(engine_dir, p)) == 0]
        report["checkpointsOk"] = len(expected) >= 3 and not missing

        ffmpeg = os.path.join(engine_dir, "..", "bin", "ffmpeg.exe")
        result = subprocess.run([ffmpeg, "-version"], capture_output=True, timeout=30, check=False)
        report["ffmpegOk"] = result.returncode == 0

        report["ok"] = report["checkpointsOk"] and report["ffmpegOk"]
    except Exception as error:  # noqa: BLE001 - the probe reports, never raises
        report["error"] = f"{type(error).__name__}: {error}"

    print(json.dumps(report))
    return 0 if report["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
