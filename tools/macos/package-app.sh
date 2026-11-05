#!/usr/bin/env bash
# Builds Scion.app for one kind of Mac and zips it. Run it on macOS (it needs codesign, plutil and ditto), from the repository root:
#
#   bash tools/macos/package-app.sh <runtime id> <version> <zip file>
#   bash tools/macos/package-app.sh osx-arm64 0.1.1 out/Scion-osx-arm64.zip
#
# osx-arm64 is Apple silicon, osx-x64 is Intel. Used by package-macos.yml (a package to try) and by release.yml (the release's zip).
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "Usage: package-app.sh <runtime id> <version> <zip file>" >&2
  exit 2
fi

rid="$1"
version="$2"
zip_file="$3"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
publish="$work/publish"
app="$work/Scion.app"

dotnet publish src/Scion.Host.Desktop -c Release -r "$rid" --self-contained -o "$publish"

# An Apple silicon Mac refuses to run native code without a signature. These are ad hoc signatures: there is no developer identity,
# so Gatekeeper still asks the first time (see "Unsigned" in the README). The signing and the checks happen here, in the folder the
# program was published to, before the bundle exists: inside a bundle, codesign takes the program for the bundle's main executable
# and wants the bundle's resources sealed too, which it cannot do here (the managed .dll files in Contents/MacOS count as unsigned
# code, and a folder such as wwwroot/_content/Scion.UI as a nested bundle). The program itself is signed by the .NET SDK when it is
# published on a Mac; the check fails the build if that ever stops being so, rather than ship a program the Mac would kill.
find "$publish" -type f -name '*.dylib' -exec codesign --force --sign - {} +
find "$publish" -type f -name '*.dylib' -exec codesign --verify --strict {} +
codesign --verify --strict "$publish/Scion.Host.Desktop"

mkdir -p "$app/Contents/MacOS"
cp -R "$publish/." "$app/Contents/MacOS/"
chmod +x "$app/Contents/MacOS/Scion.Host.Desktop"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>Scion</string>
  <key>CFBundleDisplayName</key><string>Scion</string>
  <key>CFBundleIdentifier</key><string>scion.desktop</string>
  <key>CFBundleExecutable</key><string>Scion.Host.Desktop</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${version}</string>
  <key>CFBundleVersion</key><string>${version}</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
</dict>
</plist>
PLIST
plutil -lint "$app/Contents/Info.plist"

mkdir -p "$(dirname "$zip_file")"
rm -f "$zip_file"
ditto -c -k --keepParent "$app" "$zip_file"
echo "Wrote $zip_file"
