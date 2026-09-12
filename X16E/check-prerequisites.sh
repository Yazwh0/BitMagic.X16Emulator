#!/usr/bin/env bash
# Checks the Linux runtime prerequisites for BitMagic - The Emulator (X16E).
# Run this from the extracted release folder (it looks for the other files next to itself).
set -uo pipefail

pass=0
fail=0

ok()   { printf '  [OK]      %s\n' "$1"; pass=$((pass+1)); }
bad()  { printf '  [MISSING] %s\n' "$1"; [ -n "${2:-}" ] && printf '            %s\n' "$2"; fail=$((fail+1)); }

echo "BitMagic - The Emulator: prerequisite check"
echo "============================================"

# --- OS sanity ---
if [ "$(uname -s)" != "Linux" ]; then
    echo "This script is for Linux hosts only (detected: $(uname -s))."
    exit 1
fi

# --- .NET runtime ---
# X16E targets net6.0 but is framework-dependent, and .NET's default roll-forward
# policy runs a net6.0 app on any later installed major too (not just 6.x) -- which
# matters since .NET 6 itself is EOL and many machines will only have a newer one.
# So this checks for "highest installed Microsoft.NETCore.App >= 6.0", not an exact 6.x match.
echo
echo ".NET runtime:"
if command -v dotnet >/dev/null 2>&1; then
    highest=$(dotnet --list-runtimes 2>/dev/null | awk '/^Microsoft\.NETCore\.App /{print $2}' | sort -V | tail -n1)
    if [ -n "$highest" ] && [ "$(printf '%s\n6.0.0\n' "$highest" | sort -V | head -n1)" = "6.0.0" ]; then
        ok ".NET runtime found ($highest -- roll-forward will use this for this net6.0 app)"
    else
        bad "no Microsoft.NETCore.App runtime >= 6.0 found" \
            "install: https://learn.microsoft.com/dotnet/core/install/linux"
    fi
else
    bad "'dotnet' command not found" \
        "install: https://learn.microsoft.com/dotnet/core/install/linux"
fi

# --- shared libraries ---
echo
echo "Native libraries:"

find_lib() {
    # $1: display name, $2: grep pattern (BRE, may use \| alternation), $3: apt package suggestion
    if ldconfig -p 2>/dev/null | grep -qi "$2"; then
        ok "$1"
    else
        bad "$1 not found" "install (Debian/Ubuntu): sudo apt install $3   (package name may differ on other distros)"
    fi
}

find_lib "SDL2 (windowing/input)" 'libSDL2-2\.0\.so\|libSDL2\.so' "libsdl2-2.0-0"
find_lib "OpenGL"                 'libGL\.so'                     "libgl1 mesa-utils"
find_lib "OpenAL (audio)"         'libopenal\.so'                 "libopenal1"

# --- bundled native components ---
echo
echo "Bundled components (should ship with this build):"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

check_bundled() {
    local name="$1" file="$2"
    if [ -f "$script_dir/$file" ]; then
        ok "$name found next to the executable"
    else
        bad "$name not found next to the executable" "the release tarball may have been extracted incompletely -- re-extract it"
    fi
}

check_bundled "EmulatorCore.so"    "EmulatorCore.so"
check_bundled "libzimodem_host.so" "libzimodem_host.so"

echo
echo "============================================"
echo "$pass check(s) passed, $fail missing."
if [ "$fail" -gt 0 ]; then
    echo "Install the missing items above, then re-run this script."
    exit 1
fi
echo "All prerequisites look present."
exit 0
