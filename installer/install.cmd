@echo off
setlocal
rem ============================================================================
rem  Steel Coating Takeoff - installer payload script (run by the setup .exe).
rem  Extracts the app to the user's LocalAppData (no admin needed) and creates
rem  Start Menu + Desktop shortcuts.
rem ============================================================================

set "APPNAME=Steel Coating Takeoff"
set "TARGET=%LOCALAPPDATA%\Programs\SteelCoatingTakeoff"
set "PAYLOAD=%~dp0SteelCoatingTakeoff.zip"

echo Installing %APPNAME% to:
echo   %TARGET%
echo.

if exist "%TARGET%" (
  echo Removing previous version...
  rmdir /s /q "%TARGET%" 2>nul
)
mkdir "%TARGET%" 2>nul

echo Extracting files (this includes the bundled Sage SDK, please wait)...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Expand-Archive -LiteralPath '%PAYLOAD%' -DestinationPath '%TARGET%' -Force"
if errorlevel 1 (
  echo.
  echo ERROR: extraction failed.
  pause
  exit /b 1
)

rem The zip contains a top-level SteelCoatingTakeoff folder; flatten it.
if exist "%TARGET%\SteelCoatingTakeoff\SteelCoatingTakeoff.exe" (
  robocopy "%TARGET%\SteelCoatingTakeoff" "%TARGET%" /E /MOVE >nul
)

set "EXE=%TARGET%\SteelCoatingTakeoff.exe"
if not exist "%EXE%" (
  echo.
  echo ERROR: %EXE% not found after extraction.
  pause
  exit /b 1
)

echo Creating shortcuts...
set "SM=%APPDATA%\Microsoft\Windows\Start Menu\Programs"
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "$w=New-Object -ComObject WScript.Shell;" ^
  "foreach($p in @('%SM%\%APPNAME%.lnk','%USERPROFILE%\Desktop\%APPNAME%.lnk')){" ^
  "  $s=$w.CreateShortcut($p); $s.TargetPath='%EXE%';" ^
  "  $s.WorkingDirectory='%TARGET%'; $s.Description='%APPNAME%'; $s.Save() }"

echo.
echo ============================================================
echo  %APPNAME% installed.
echo  Launch it from the Start Menu or Desktop shortcut, or run:
echo    %EXE%
echo ============================================================
echo.

choice /C YN /N /M "Launch %APPNAME% now? [Y/N] "
if errorlevel 2 goto :done
start "" "%EXE%"
:done
endlocal
