"""Compatibility launcher for opening the Godot project from older workflows."""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parent
MACOS_GODOT = Path("/Applications/Godot.app/Contents/MacOS/Godot")


def find_godot() -> str | None:
    configured_path = os.environ.get("GODOT_EXECUTABLE")
    if configured_path and Path(configured_path).is_file():
        return configured_path

    for executable in ("godot", "godot4", "Godot.exe", "godot.exe"):
        located = shutil.which(executable)
        if located:
            return located

    if MACOS_GODOT.is_file():
        return str(MACOS_GODOT)

    return None


def main() -> int:
    godot = find_godot()
    if godot is None:
        print(
            "Godot 4 wurde nicht gefunden. Installiere Godot 4 und öffne "
            f"{PROJECT_ROOT / 'project.godot'} im Godot-Projektmanager. "
            "Alternativ kannst du den Pfad zur ausführbaren Datei über "
            "GODOT_EXECUTABLE setzen.",
            file=sys.stderr,
        )
        return 1

    command = [godot, "--path", str(PROJECT_ROOT), *sys.argv[1:]]
    try:
        return subprocess.run(command, check=False).returncode
    except OSError as error:
        print(f"Godot konnte nicht gestartet werden: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
