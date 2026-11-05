#!/usr/bin/env bash
# Makes the signed and notarized Mac package of Scion with Velopack. Run it on macOS (it needs codesign, xcrun and the vpk tool, of the
# same version as the Velopack package in Directory.Packages.props), from the repository root, after signing-keychain.sh create:
#
#   bash tools/macos/pack.sh <runtime id> <version> [release notes file]
#   bash tools/macos/pack.sh osx-arm64 0.2.0 docs/releases/0.2.0.md
#
# osx-arm64 is Apple silicon, osx-x64 is Intel. It reads SCION_KEYCHAIN and SCION_SIGN_IDENTITY (left by signing-keychain.sh) and
# prints neither. The result is in Releases/: Scion-<runtime id>-Portable.zip (the app, signed, notarized and stapled: the file people
# download), the package Scion updates itself from (Scion-<version>-<runtime id>-full.nupkg) and releases.<runtime id>.json. The
# runtime id is also Velopack's channel, so the two kinds of Mac keep separate feeds and Windows keeps its own.
set -euo pipefail

if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  echo "Usage: pack.sh <runtime id> <version> [release notes file]" >&2
  exit 2
fi

rid="$1"
version="$2"
notes="${3:-}"
: "${SCION_SIGN_IDENTITY:?is not set: run signing-keychain.sh create first}" "${SCION_KEYCHAIN:?is not set: run signing-keychain.sh create first}"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

dotnet publish src/Scion.Host.Desktop -c Release -r "$rid" --self-contained -o "$work/publish"

# vpk signs every file with the hardened runtime, builds Scion.app, notarizes it (notarytool, with the profile signing-keychain.sh stored
# in the keychain), staples the ticket and checks the result with spctl. The entitlements are Scion's own, not Velopack's defaults: see
# "macOS signing" in CLAUDE.md for why each of the others is left out.
arguments=(
  -u Scion
  -v "$version"
  -p "$work/publish"
  -e Scion.Host.Desktop
  --packTitle Scion
  --channel "$rid"
  --bundleId scion.desktop
  --icon src/Scion.Host.Desktop/Assets/scion.icns
  --noInst
  --signAppIdentity "$SCION_SIGN_IDENTITY"
  --signEntitlements tools/macos/Scion.entitlements
  --notaryProfile scion-notary
  --keychain "$SCION_KEYCHAIN"
)
if [ -n "$notes" ]; then arguments+=(--releaseNotes "$notes"); fi
vpk pack "${arguments[@]}"

# What is checked here is the app inside the zip people download, not the one vpk made on the way.
zip="Releases/Scion-$rid-Portable.zip"
if [ ! -f "$zip" ]; then
  echo "$zip is missing: the website's download link goes to this name." >&2
  exit 1
fi

ditto -x -k "$zip" "$work/unzipped"
app="$(find "$work/unzipped" -maxdepth 2 -name '*.app' -type d | head -n 1)"
if [ -z "$app" ]; then
  echo "$zip holds no .app." >&2
  exit 1
fi

codesign --verify --deep --strict "$app"
xcrun stapler validate "$app"
echo "Made $zip"
