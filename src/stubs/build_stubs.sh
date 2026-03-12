#!/bin/bash
set -e

NDK=~/Android/Sdk/ndk/28.1.13356709
CC=$NDK/toolchains/llvm/prebuilt/linux-x86_64/bin/aarch64-linux-android24-clang
OUT=out/arm64-v8a

mkdir -p $OUT

$CC -shared -o $OUT/libsteam_api.so steam_stub.c steam_stub_auto.c -Wl,-soname,libsteam_api.so
$CC -shared -o "$OUT/libsentry.so" sentry_stub.c

ls -lh $OUT/
