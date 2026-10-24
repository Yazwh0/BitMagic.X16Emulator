#!/usr/bin/env bash
# Downloads the latest Commander X16 ROM image from the X16Community/x16-rom
# GitHub releases page and installs rom.bin into the user's BitMagic folder
# ($XDG_DATA_HOME/BitMagic, or ~/.local/share/BitMagic), where X16E looks for it,
# or into the directory passed as the first argument. X16E --fetch-rom runs this.
#
# Deliberately avoids the api.github.com REST API (its unauthenticated rate
# limit is a low 60 requests/hour per IP, shared with anything else on that
# IP -- easy to exhaust). Instead this resolves the latest release via the
# plain github.com redirect and scrapes the release page's asset list, both
# ordinary page loads that aren't subject to that quota.
set -euo pipefail

repo="X16Community/x16-rom"
dest_dir="${1:-${XDG_DATA_HOME:-$HOME/.local/share}/BitMagic}"

for tool in curl unzip; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "$tool not found on PATH. Install it (e.g. sudo apt install $tool) and re-run." >&2
        exit 1
    fi
done

echo "Resolving latest ROM release for $repo..."
tag_url=$(curl -fsSL -o /dev/null -w '%{url_effective}' -L "https://github.com/$repo/releases/latest")
tag="${tag_url##*/}"

if [ -z "$tag" ]; then
    echo "Could not resolve the latest release tag for $repo." >&2
    exit 1
fi

echo "Latest ROM release: $tag"

assets_html=$(curl -fsSL "https://github.com/$repo/releases/expanded_assets/$tag")
asset_path=$(printf '%s' "$assets_html" | grep -o "href=\"/$repo/releases/download/[^\"]*\.zip\"" | head -n1 | sed -E 's/^href="(.*)"$/\1/')

if [ -z "$asset_path" ]; then
    echo "Could not find a ROM .zip asset in release $tag of $repo." >&2
    exit 1
fi

asset_url="https://github.com$asset_path"
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
