#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
GODOT_DIR="$ROOT/vendor/godot"
ANDROID_LIBS="$ROOT/android/libs/release"

export ANDROID_HOME="${ANDROID_HOME:-$HOME/Android/Sdk}"
if [ -z "${ANDROID_NDK_ROOT:-}" ]; then
    LATEST_NDK=$(ls -1 "$ANDROID_HOME/ndk" | sort -V | tail -1)
    export ANDROID_NDK_ROOT="$ANDROID_HOME/ndk/$LATEST_NDK"
fi

source "$ROOT/venv/bin/activate"

# Build Godot for Android arm64
echo "Building Godot (android arm64 template_release)..."
cd "$GODOT_DIR"
scons platform=android arch=arm64 target=template_release module_mono_enabled=yes -j$(nproc)

BUILT_SO="$GODOT_DIR/platform/android/java/lib/libs/release/arm64-v8a/libgodot_android.so"
if [ ! -f "$BUILT_SO" ]; then
    echo "ERROR: Expected output not found at $BUILT_SO"
    exit 1
fi

# Update the .so inside the AAR
echo "Updating libgodot_android.so in AAR..."
TMPDIR=$(mktemp -d)
mkdir -p "$TMPDIR/jni/arm64-v8a"
cp "$BUILT_SO" "$TMPDIR/jni/arm64-v8a/libgodot_android.so"
(cd "$TMPDIR" && zip -u "$ANDROID_LIBS/godot-lib.template_release.aar" jni/arm64-v8a/libgodot_android.so)
rm -rf "$TMPDIR"

cp "$BUILT_SO" "$ANDROID_LIBS/arm64-v8a/libgodot_android.so"

echo "Godot engine rebuild complete!"
