#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PATCHER_DIR="$ROOT/src/STS2Mobile"
BUILD_DIR="$ROOT/android"
GRADLE_PROPS="$BUILD_DIR/gradle.properties"
APK_DIR="$BUILD_DIR/build/outputs/apk/mono/release"

# 1. Format
echo "Formatting C# code..."
~/.dotnet/tools/csharpier format "$PATCHER_DIR"

# 2. Build patcher
echo "Building patcher..."
cd "$PATCHER_DIR"
dotnet publish -c Release

PUBLISH_DIR="$PATCHER_DIR/bin/Release/net9.0/publish"
BCL_DIR="$BUILD_DIR/assets/dotnet_bcl"
mkdir -p "$BCL_DIR"

cp "$PUBLISH_DIR"/STS2Mobile.dll "$PUBLISH_DIR"/SteamKit2.dll \
   "$PUBLISH_DIR"/protobuf-net.dll "$PUBLISH_DIR"/protobuf-net.Core.dll \
   "$PUBLISH_DIR"/System.IO.Hashing.dll "$PUBLISH_DIR"/ZstdSharp.dll \
   "$BCL_DIR/"

cp "$ROOT/upstream/godot-export/.godot/mono/publish/arm64/GodotSharp.dll" "$BCL_DIR/"

CRYPTO_SO="$HOME/.nuget/packages/microsoft.netcore.app.runtime.mono.android-arm64/9.0.7/runtimes/android-arm64/native/libSystem.Security.Cryptography.Native.Android.so"
if [ -f "$CRYPTO_SO" ]; then
    cp "$CRYPTO_SO" "$BUILD_DIR/libs/release/arm64-v8a/"
fi

echo "Copied patcher + dependencies to android assets"

# 3. Bump version
CURRENT_NAME=$(grep '^export_version_name=' "$GRADLE_PROPS" | cut -d= -f2)
CURRENT_CODE=$(grep '^export_version_code=' "$GRADLE_PROPS" | cut -d= -f2)

IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_NAME"
PATCH=$((PATCH + 1))
NEW_NAME="$MAJOR.$MINOR.$PATCH"
NEW_CODE=$((CURRENT_CODE + 1))

sed -i "s/^export_version_name=.*/export_version_name=$NEW_NAME/" "$GRADLE_PROPS"
sed -i "s/^export_version_code=.*/export_version_code=$NEW_CODE/" "$GRADLE_PROPS"
echo "Version: $CURRENT_NAME ($CURRENT_CODE) -> $NEW_NAME ($NEW_CODE)"

# 4. Build APK
echo "Building APK..."
cd "$BUILD_DIR"
./gradlew assembleMonoRelease

echo "Done: $APK_DIR/StS2Launcher-v$NEW_NAME.apk"
