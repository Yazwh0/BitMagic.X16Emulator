#!/usr/bin/env bash
# Checks the Linux runtime prerequisites for BitMagic - The Emulator (X16E). The debugger
# (X16D) shares the same native code, so the same requirements apply to it.
# Run this from the extracted release folder (it looks for the other files next to itself).
#
# Versions below come from the release binaries themselves (objdump -T / readelf -d):
#   glibc 2.34      - the bundled libglfw.so.3 needs GLIBC_2.34 (libzimodem_host.so 2.33)
#   GLIBCXX_3.4.29  - libzimodem_host.so, i.e. libstdc++ from GCC 11 or later
# Everything else is dlopen'd at runtime, so isn't visible to ldd: Silk.NET loads
# libopenal.so.1 and libGL.so.1, and GLFW loads either the X11 or the Wayland libraries.
# SDL2 and GLFW themselves are bundled with the build.
set -uo pipefail

pass=0
fail=0
warn=0

ok()   { printf '  [OK]      %s\n' "$1"; pass=$((pass+1)); }
bad()  { printf '  [MISSING] %s\n' "$1"; [ -n "${2:-}" ] && printf '            %s\n' "$2"; fail=$((fail+1)); }
note() { printf '  [WARNING] %s\n' "$1"; [ -n "${2:-}" ] && printf '            %s\n' "$2"; warn=$((warn+1)); }

# Version comparison: true if $1 >= $2.
version_ge() { [ "$(printf '%s\n%s\n' "$1" "$2" | sort -V | head -n1)" = "$2" ]; }

echo "BitMagic - The Emulator: prerequisite check"
echo "============================================"

# --- OS sanity ---
if [ "$(uname -s)" != "Linux" ]; then
    echo "This script is for Linux hosts only (detected: $(uname -s))."
    exit 1
fi

echo
echo "System:"

arch="$(uname -m)"
if [ "$arch" = "x86_64" ]; then
    ok "x64 CPU"
else
    bad "x64 CPU required (detected: $arch)" "the emulator core is written in x64 assembly, so other architectures can't run it"
fi

glibc="$(getconf GNU_LIBC_VERSION 2>/dev/null | awk '{print $2}')"
if [ -z "$glibc" ]; then
    bad "glibc not detected" "a glibc-based distro is required (musl distros such as Alpine aren't supported)"
elif version_ge "$glibc" "2.34"; then
    ok "glibc $glibc (2.34 or later needed)"
else
    bad "glibc $glibc is too old (2.34 or later needed)" "upgrade to Ubuntu 22.04, Debian 12, Fedora 35, RHEL 9 or later"
fi

# ldconfig isn't on a normal user's PATH on some distros (Debian), so look in sbin too.
ldconfig_bin="$(command -v ldconfig 2>/dev/null || true)"
for candidate in /sbin/ldconfig /usr/sbin/ldconfig; do
    [ -z "$ldconfig_bin" ] && [ -x "$candidate" ] && ldconfig_bin="$candidate"
done
libcache=""
[ -n "$ldconfig_bin" ] && libcache="$("$ldconfig_bin" -p 2>/dev/null)"

# Path of an x86-64 library by exact soname, or empty.
lib_path() { printf '%s\n' "$libcache" | awk -v n="$1" '$1 == n && /x86-64/ { print $NF; exit }'; }
has_lib()  { [ -n "$(lib_path "$1")" ]; }

libstdcxx="$(lib_path libstdc++.so.6)"
if [ -z "$libstdcxx" ]; then
    bad "libstdc++ not found" "install (Debian/Ubuntu): sudo apt install libstdc++6"
elif grep -aq 'GLIBCXX_3\.4\.29' "$libstdcxx"; then
    ok "libstdc++ (GCC 11 or later)"
else
    bad "libstdc++ is too old (GCC 11 or later needed)" "upgrade to Ubuntu 22.04, Debian 12, Fedora 35, RHEL 9 or later"
fi

# --- .NET runtime ---
# X16E targets net10.0 but is framework-dependent, and .NET's default roll-forward
# policy runs it on any later installed major too (not just 10.x) -- so this checks
# for "highest installed Microsoft.NETCore.App >= 10.0", not an exact 10.x match.
echo
echo ".NET runtime:"
dotnet_hint="install: https://learn.microsoft.com/dotnet/core/install/linux (not needed for the VSCode extension if it downloads its own .NET, its default)"
if command -v dotnet >/dev/null 2>&1; then
    highest=$(dotnet --list-runtimes 2>/dev/null | awk '/^Microsoft\.NETCore\.App /{print $2}' | sort -V | tail -n1)
    if [ -n "$highest" ] && version_ge "$highest" "10.0.0"; then
        ok ".NET runtime found ($highest -- roll-forward will use this for this net10.0 app)"
    else
        bad "no Microsoft.NETCore.App runtime >= 10.0 found" "$dotnet_hint"
    fi
else
    bad "'dotnet' command not found" "$dotnet_hint"
fi

# --- shared libraries ---
echo
echo "Native libraries:"

if [ -z "$libcache" ]; then
    note "ldconfig not found, so the library checks below can't be trusted"
fi

find_lib() {
    # $1: display name, $2: soname, $3: apt package suggestion
    if has_lib "$2"; then
        ok "$1"
    else
        bad "$1 not found ($2)" "install (Debian/Ubuntu): sudo apt install $3   (package name may differ on other distros)"
    fi
}

find_lib "OpenGL"         libGL.so.1     "libgl1 libgl1-mesa-dri"
find_lib "OpenAL (audio)" libopenal.so.1 "libopenal1"

# --- display ---
# GLFW (bundled) draws the window and uses either X11 or Wayland, so one complete set is enough.
echo
echo "Display (X11 or Wayland):"

x11_missing=""
for l in libX11.so.6; do has_lib "$l" || x11_missing="$x11_missing $l"; done
x11_optional_missing=""
for l in libXcursor.so.1 libXrandr.so.2 libXi.so.6 libXinerama.so.1; do has_lib "$l" || x11_optional_missing="$x11_optional_missing $l"; done
wayland_missing=""
for l in libwayland-client.so.0 libwayland-cursor.so.0 libwayland-egl.so.1 libxkbcommon.so.0; do has_lib "$l" || wayland_missing="$wayland_missing $l"; done

if [ -z "$x11_missing" ] || [ -z "$wayland_missing" ]; then
    [ -z "$x11_missing" ] && ok "X11 libraries"
    [ -z "$wayland_missing" ] && ok "Wayland libraries"
    if [ -z "$x11_missing" ] && [ -n "$x11_optional_missing" ] && [ -n "$wayland_missing" ]; then
        note "some optional X11 libraries are missing:$x11_optional_missing" \
             "install (Debian/Ubuntu): sudo apt install libxcursor1 libxrandr2 libxi6 libxinerama1"
    fi
else
    bad "neither the X11 nor the Wayland libraries were found" \
        "install (Debian/Ubuntu): sudo apt install libx11-6 libxcursor1 libxrandr2 libxi6 libxinerama1"
fi

if [ -n "${DISPLAY:-}" ] || [ -n "${WAYLAND_DISPLAY:-}" ]; then
    ok "display available (${WAYLAND_DISPLAY:+WAYLAND_DISPLAY=$WAYLAND_DISPLAY }${DISPLAY:+DISPLAY=$DISPLAY})"
else
    note "no display found (neither DISPLAY nor WAYLAND_DISPLAY is set)" \
         "fine if you're checking over SSH, but the emulator window needs one; under WSL, use WSLg (Windows 11)"
fi

# --- ROM download (fetch-rom.sh / X16E --fetch-rom) ---
echo
echo "ROM download tools:"

find_cmd() {
    # $1: command, $2: apt package suggestion
    if command -v "$1" >/dev/null 2>&1; then
        ok "$1"
    else
        bad "$1 not found" "install (Debian/Ubuntu): sudo apt install $2"
    fi
}

find_cmd curl  curl
find_cmd unzip unzip

# --- bundled native components ---
echo
echo "Bundled components (should ship with this build):"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

check_bundled() {
    # $1: display name, then one or more paths relative to this script; any one will do.
    local name="$1"; shift
    local f
    for f in "$@"; do
        if [ -f "$script_dir/$f" ]; then
            ok "$name found"
            return
        fi
    done
    bad "$name not found next to the executable" "the release tarball may have been extracted incompletely -- re-extract it"
}

check_bundled "EmulatorCore.so"    "EmulatorCore.so"
check_bundled "libzimodem_host.so" "libzimodem_host.so"
check_bundled "SDL2"               "runtimes/linux-x64/native/libSDL2-2.0.so" "libSDL2-2.0.so"
check_bundled "GLFW"               "runtimes/linux-x64/native/libglfw.so.3"   "libglfw.so.3"

echo
echo "============================================"
echo "$pass check(s) passed, $fail missing, $warn warning(s)."
if [ "$fail" -gt 0 ]; then
    echo "Install the missing items above, then re-run this script."
    exit 1
fi
echo "All prerequisites look present."
exit 0
