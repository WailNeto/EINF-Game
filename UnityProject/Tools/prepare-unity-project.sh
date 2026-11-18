#!/bin/sh
set -eu

project_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
editor=${UNITY_EDITOR_PATH:-/Applications/Unity/Hub/Editor/2022.3.17f1/Unity.app/Contents/MacOS/Unity}
urp_package="$project_dir/Library/PackageCache/com.unity.render-pipelines.universal@14.0.9"
folder_meta="$urp_package/ShaderLibrary/Debug.meta"
script_meta="$urp_package/ShaderLibrary/Debug/DebugViewEnums.cs.meta"
restore_log="$project_dir/Library/Logs/phase0-package-restore.log"
setup_log="$project_dir/Library/Logs/phase0-project-setup.log"

repair_urp_metadata() {
    if ! grep -q '^folderAsset: yes$' "$folder_meta"; then
        printf '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' >> "$folder_meta"
    fi

    if ! grep -q '^MonoImporter:' "$script_meta"; then
        printf '\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' >> "$script_meta"
    fi
}

if [ ! -x "$editor" ]; then
    printf 'Unity Editor executable not found: %s\nSet UNITY_EDITOR_PATH to its path.\n' "$editor" >&2
    exit 1
fi

if ! editor_version=$("$editor" -version 2>&1); then
    printf 'Unity Editor check failed for %s:\n%s\n' "$editor" "$editor_version" >&2
    exit 1
fi

case "$editor_version" in
    [0-9][0-9][0-9][0-9].[0-9]*)
        printf 'Using Unity Editor %s\n' "$editor_version"
        ;;
    *)
        printf 'The selected executable is not a Unity Editor: %s\n%s\n' "$editor" "$editor_version" >&2
        exit 1
        ;;
esac

if [ ! -f "$script_meta" ]; then
    mkdir -p "$project_dir/Library/Logs"
    if ! "$editor" -batchmode -nographics -projectPath "$project_dir" -quit -logFile "$restore_log"; then
        if ! grep -q 'HDRDebugMode' "$restore_log"; then
            printf 'Unity project initialization failed; inspect %s\n' "$restore_log" >&2
            exit 1
        fi
    fi
fi

if [ ! -f "$folder_meta" ] || [ ! -f "$script_meta" ]; then
    printf 'The installed editor did not provide the expected URP 14.0.9 package files.\n' >&2
    exit 1
fi

repair_urp_metadata

if ! "$editor" -batchmode -nographics -projectPath "$project_dir" \
    -executeMethod DinoGenesis.Editor.ProjectBootstrap.ConfigureProject \
    -quit -logFile "$setup_log"; then
    printf 'Unity Phase 0 setup failed; inspect %s\n' "$setup_log" >&2
    exit 1
fi

repair_urp_metadata

if [ "${1:-}" = "--setup-only" ]; then
    exit 0
fi

exec "$editor" -projectPath "$project_dir" "$@"
