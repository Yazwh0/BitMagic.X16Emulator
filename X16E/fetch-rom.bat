@echo off
rem Downloads the latest Commander X16 ROM image from the X16Community/x16-rom
rem GitHub releases page and installs rom.bin next to this script, or into the
rem directory passed as the first argument.
rem
rem Deliberately avoids the api.github.com REST API (its unauthenticated rate
rem limit is a low 60 requests/hour per IP, shared with anything else on that
rem IP -- easy to exhaust). Instead this resolves the latest release via the
rem plain github.com redirect and scrapes the release page's asset list, both
rem ordinary page loads that aren't subject to that quota.
setlocal EnableDelayedExpansion

set "REPO=X16Community/x16-rom"

if "%~1"=="" (
    set "DEST_DIR=%~dp0"
) else (
    set "DEST_DIR=%~1"
)

where curl >nul 2>&1
if errorlevel 1 (
    echo curl.exe not found on PATH. It ships with Windows 10 1803+ / Server 2019+. 1>&2
    exit /b 1
)
where tar >nul 2>&1
if errorlevel 1 (
    echo tar.exe not found on PATH. It ships with Windows 10 1803+ / Server 2019+. 1>&2
    exit /b 1
)

set "TMP_DIR=%TEMP%\fetch-rom-%RANDOM%%RANDOM%"
mkdir "%TMP_DIR%" >nul 2>&1

echo Resolving latest ROM release for %REPO%...
for /f "usebackq delims=" %%A in (`curl -fsSL -o nul -w "%%{url_effective}" -L "https://github.com/%REPO%/releases/latest"`) do set "TAG_URL=%%A"

if not defined TAG_URL (
    echo Failed to resolve the latest release for %REPO%. 1>&2
    goto :fail
)

set "TAG=!TAG_URL:*/tag/=!"

if not defined TAG (
    echo Could not parse a release tag from !TAG_URL! 1>&2
    goto :fail
)

echo Latest ROM release: !TAG!

curl -fsSL -o "%TMP_DIR%\assets.html" "https://github.com/%REPO%/releases/expanded_assets/!TAG!"
if errorlevel 1 (
    echo Failed to fetch the asset list for release !TAG! of %REPO%. 1>&2
    goto :fail
)

set "ASSET_LINE="
for /f "usebackq delims=" %%A in (`findstr /r /c:"releases/download/!TAG!/.*\.zip" "%TMP_DIR%\assets.html"`) do (
    if not defined ASSET_LINE set "ASSET_LINE=%%A"
)

if not defined ASSET_LINE (
    echo Could not find a ROM .zip asset in release !TAG! of %REPO%. 1>&2
    goto :fail
)

rem Strip everything up to and including "releases/download/<tag>/", leaving
rem the filename followed by the closing quote and any other HTML attributes.
set "STEP1=!ASSET_LINE:*releases/download/%TAG%/=!"
rem Everything from (and including) the first ".zip" onward.
set "AFTER=!STEP1:*.zip=!"

rem Both strings may contain literal quote characters, which breaks quoted
rem CALL arguments -- hand them to :strlen via a plain global instead.
set "_S=!STEP1!"
call :strlen LEN1
set "_S=!AFTER!"
call :strlen LEN2

set /a FNLEN=LEN1-LEN2
for /f %%N in ("!FNLEN!") do set "ASSET_NAME=!STEP1:~0,%%N!"

if not defined ASSET_NAME (
    echo Could not determine the ROM asset filename for release !TAG! of %REPO%. 1>&2
    goto :fail
)

set "ASSET_URL=https://github.com/%REPO%/releases/download/!TAG!/!ASSET_NAME!"
echo Downloading !ASSET_URL!

curl -fsSL -o "%TMP_DIR%\rom.zip" "!ASSET_URL!"
if errorlevel 1 (
    echo Failed to download !ASSET_URL! 1>&2
    goto :fail
)

tar -xf "%TMP_DIR%\rom.zip" -C "%TMP_DIR%" rom.bin
if errorlevel 1 (
    echo Failed to extract rom.bin from the downloaded archive. 1>&2
    goto :fail
)

if not exist "%TMP_DIR%\rom.bin" (
    echo rom.bin not found inside !ASSET_URL! 1>&2
    goto :fail
)

if not exist "%DEST_DIR%" mkdir "%DEST_DIR%"
copy /y "%TMP_DIR%\rom.bin" "%DEST_DIR%\rom.bin" >nul

echo Installed rom.bin (!TAG!) to %DEST_DIR%\rom.bin

call :cleanup
endlocal
exit /b 0

:fail
call :cleanup
endlocal
exit /b 1

:cleanup
if exist "%TMP_DIR%" rd /s /q "%TMP_DIR%"
goto :eof

:strlen
rem Reads the string to measure from the global %_S%, returns the length in
rem the variable named by %1. Deliberately doesn't take the string itself as
rem a parameter -- it may contain quote characters, which corrupts quoted
rem CALL arguments.
setlocal EnableDelayedExpansion
set "s=!_S!"
set "len=0"
for %%P in (4096 2048 1024 512 256 128 64 32 16 8 4 2 1) do (
    if not "!s:~%%P,1!"=="" (
        set /a "len+=%%P"
        set "s=!s:~%%P!"
    )
)
endlocal & set "%~1=%len%"
goto :eof
