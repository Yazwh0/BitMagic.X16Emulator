#!/usr/bin/env bash
# Downloads the latest Commander X16 ROM image from the X16Community/x16-rom
# GitHub releases page and installs rom.bin next to this script, or into the
# directory passed as the first argument.
set -euo pipefail

repo="X16Community/x16-rom"
dest_dir="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)}"

echo "Fetching latest ROM release info for $repo..."
release_json=$(curl -fsSL -H "Accept: application/vnd.github+json" "https://api.github.com/repos/$repo/releases/latest")

tag=$(printf '%s' "$release_json" | grep -o '"tag_name" *: *"[^"]*"' | head -n1 | sed -E 's/.*"([^"]+)"$/\1/')
asset_url=$(printf '%s' "$release_json" | grep -o '"browser_download_url" *: *"[^"]*\.zip"' | head -n1 | sed -E 's/.*"(https[^"]+)"/\1/')

if [ -z "$asset_url" ]; then
    echo "Could not find a ROM .zip asset in the latest release of $repo." >&2
    exit 1
fi

echo "Latest ROM release: $tag"
echo "Downloading $asset_url"

tmp_dir=$(mktemp -d)
trap 'rm -rf "$tmp_dir"' EXIT

curl -fsSL -o "$tmp_dir/rom.zip" "$asset_url"
unzip -o -j "$tmp_dir/rom.zip" "rom.bin" -d "$tmp_dir" >/dev/null

if [ ! -f "$tmp_dir/rom.bin" ]; then
    echo "rom.bin not found inside $asset_url" >&2
    exit 1
fi

mkdir -p "$dest_dir"
cp "$tmp_dir/rom.bin" "$dest_dir/rom.bin"

echo "Installed rom.bin ($tag) to $dest_dir/rom.bin"
