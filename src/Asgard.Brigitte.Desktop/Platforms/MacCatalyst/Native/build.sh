#!/bin/sh
set -eu
output="$1"
identity="$2"
mkdir -p "$(dirname "$output")"
xcrun --sdk macosx clang -dynamiclib -fobjc-arc -framework AppKit -framework Foundation -arch arm64 -arch x86_64 -mmacosx-version-min=12.0 "$(dirname "$0")/BrigitteTray.m" -o "$output"
/usr/bin/codesign --force --sign "${identity:--}" "$output"
