# X16E

BitMagic's standalone Commander X16 emulator, for Windows and Linux.

## Install

```bash
dotnet tool install -g BitMagic.X16E
```

This puts `x16e` on your PATH. For an early build, add `--prerelease`.

A .NET tool doesn't auto-update. To pick up a newer version later, run:

```bash
dotnet tool update -g BitMagic.X16E
```

## A ROM

X16E needs a Commander X16 ROM. To download the latest one:

```bash
x16e --fetch-rom
```

This saves `rom.bin` to `%LOCALAPPDATA%\BitMagic` on Windows, or `~/.local/share/BitMagic` on
Linux. X16E looks for a ROM in this order:

1. `--rom <file>`
2. `rom.bin` in the current directory
3. the `BITMAGIC_ROM` environment variable (the file, or the folder containing it)
4. the folder `--fetch-rom` saves to

## Linux prerequisites

X16E needs SDL2, OpenGL and OpenAL, and `--fetch-rom` needs `curl` and `unzip`. On
Debian/Ubuntu:

```bash
sudo apt install libsdl2-2.0-0 libgl1 libopenal1 curl unzip
```

## Usage

```bash
x16e --sdcard card.vhd
x16e --prg program.prg
x16e --help
```

Full source: https://github.com/Yazwh0/BitMagic. More at https://bitmagic.org.
