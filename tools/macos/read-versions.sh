#!/usr/bin/env bash
# Prints the two versions a Mac job needs, as lines for $GITHUB_OUTPUT: the version of Scion (Directory.Build.props) and the version of
# Velopack (Directory.Packages.props), which vpk must match. Run from the repository root.
set -euo pipefail

version=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' Directory.Build.props | head -n 1)
velopack=$(sed -n 's:.*<PackageVersion Include="Velopack" Version="\([^"]*\)".*:\1:p' Directory.Packages.props | head -n 1)

if [ -z "$version" ] || [ -z "$velopack" ]; then
  echo "Could not read the versions: Scion '$version', Velopack '$velopack'." >&2
  exit 1
fi

echo "version=$version"
echo "velopack=$velopack"
