"""Launch the Dino Genesis Unity project on macOS."""

from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parent
UNITY_PROJECT_ROOT = PROJECT_ROOT / "UnityProject"
UNITY_SETUP_SCRIPT = UNITY_PROJECT_ROOT / "Tools" / "prepare-unity-project.sh"
UNITY_HUB_EDITORS = Path("/Applications/Unity/Hub/Editor")
DEFAULT_UNITY_EDITOR = (
    UNITY_HUB_EDITORS
    / "2022.3.17f1"
    / "Unity.app"
    / "Contents"
    / "MacOS"
    / "Unity"
)


def find_unity_editor() -> str | None:
    configured_path = os.environ.get("UNITY_EDITOR_PATH")
    if configured_path:
        editor = Path(configured_path).expanduser()
        if not is_unity_editor(editor):
            raise FileNotFoundError(
                f"UNITY_EDITOR_PATH ist kein Unity-Editor: {editor}. "
                "Erwartet wird Unity.app/Contents/MacOS/Unity."
            )
        return str(editor)

    installed_editors = [DEFAULT_UNITY_EDITOR]
    installed_editors.extend(
        UNITY_HUB_EDITORS.glob("*/Unity.app/Contents/MacOS/Unity")
    )
    candidates = sorted(
        {editor for editor in installed_editors if editor.is_file()},
        key=lambda editor: tuple(
            int(part) for part in re.findall(r"\d+", editor.parents[3].name)
        ),
        reverse=True,
    )
    for editor in candidates:
        if is_unity_editor(editor):
            return str(editor)

    for command_name in ("Unity", "unity"):
        located = shutil.which(command_name)
        if located and is_unity_editor(Path(located)):
            return located

    return None


def is_unity_editor(editor: Path) -> bool:
    if not editor.is_file():
        return False

    try:
        result = subprocess.run(
            [str(editor), "-version"],
            capture_output=True,
            check=False,
            text=True,
            timeout=30,
        )
    except subprocess.TimeoutExpired:
        return False

    version = (result.stdout + result.stderr).strip()
    return result.returncode == 0 and bool(
        re.search(r"(?m)^\d{4}\.\d+\.\d+[abfp]\d+$", version)
    )


def main() -> int:
    try:
        unity_editor = find_unity_editor()
    except FileNotFoundError as error:
        print(error, file=sys.stderr)
        return 1

    if unity_editor is None:
        print(
            "Unity Editor wurde nicht gefunden. Installiere Unity 2022.3.17f1 "
            "oder setze UNITY_EDITOR_PATH auf die Editor-Datei. "
            f"Das Unity-Projekt liegt unter {UNITY_PROJECT_ROOT}.",
            file=sys.stderr,
        )
        return 1

    if not UNITY_PROJECT_ROOT.is_dir() or not UNITY_SETUP_SCRIPT.is_file():
        print(
            f"Unity-Projekt oder Startskript fehlt: {UNITY_PROJECT_ROOT}",
            file=sys.stderr,
        )
        return 1

    environment = os.environ.copy()
    environment["UNITY_EDITOR_PATH"] = unity_editor
    command = ["/bin/sh", str(UNITY_SETUP_SCRIPT), *sys.argv[1:]]
    try:
        return subprocess.run(command, env=environment, check=False).returncode
    except OSError as error:
        print(f"Unity konnte nicht gestartet werden: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
