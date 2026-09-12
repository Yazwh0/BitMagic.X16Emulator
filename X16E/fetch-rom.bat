@echo off
rem Downloads the latest Commander X16 ROM image from the X16Community/x16-rom
rem GitHub releases page and installs rom.bin next to this script, or into the
rem directory passed as the first argument.
setlocal EnableDelayedExpansion

set "REPO=X16Community/x16-rom"
set Q="

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

echo Fetching latest ROM release info for %REPO%...
curl -fsSL -H "Accept: application/vnd.github+json" "https://api.github.com/repos/%REPO%/releases/latest" -o "%TMP_DIR%\release.json"
if errorlevel 1 (
    echo Failed to query the GitHub API for %REPO%. 1>&2
    goto :fail
)

set "TAG="
for /f "usebackq delims=" %%A in ("%TMP_DIR%\release.json") do (
    set "LINE=%%A"
    echo !LINE! | findstr /c:"\"tag_name\"" >nul
    if !errorlevel! equ 0 if not defined TAG call :extract TAG "!LINE!"
)

set "ASSET_URL="
for /f "usebackq delims=" %%A in ("%TMP_DIR%\release.json") do (
    set "LINE=%%A"
    echo !LINE! | findstr /r /c:"\"browser_download_url\".*\.zip" >nul
    if !errorlevel! equ 0 if not defined ASSET_URL call :extract ASSET_URL "!LINE!"
)

if not defined ASSET_URL (
    echo Could not find a ROM .zip asset in the latest release of %REPO%. 1>&2
    goto :fail
)

echo Latest ROM release: !TAG!
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

:extract
rem %1 = output variable name, %2 = the JSON line (quoted)
setlocal EnableDelayedExpansion
set "L=%~2"
for /f "tokens=1,* delims=:" %%X in ("!L!") do set "L=%%Y"
for /f "tokens=* delims= " %%X in ("!L!") do set "L=%%X"
set "L=!L:~1!"
if "!L:~-1!"=="," set "L=!L:~0,-1!"
if "!L:~-1!"=="!Q!" set "L=!L:~0,-1!"
endlocal & set "%~1=%L%"
goto :eof
