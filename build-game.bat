@echo off
:: Kuncheng RPG - build the Unity Windows player.
:: ASCII only on purpose: cmd.exe reads .bat in the OEM codepage (GBK here) and
:: mangles non-ASCII, which has already cost us one broken script.
setlocal

if "%UNITY_EXE%"=="" set UNITY_EXE=E:\2022.3.62f3c1\Editor\Unity.exe
if not exist "%UNITY_EXE%" (
  echo [build] Unity not found at "%UNITY_EXE%"
  echo [build] Set UNITY_EXE to your Unity.exe and run this again.
  exit /b 1
)

set PROJECT=%~dp0unity
set LOG=%TEMP%\kuncheng_build.log
set METHOD=KunchengRPG.EditorTools.PlayerBuilder.BuildWindowsDev
if /I "%~1"=="release" set METHOD=KunchengRPG.EditorTools.PlayerBuilder.BuildWindows

if exist "%LOG%" del "%LOG%"

echo [build] method  : %METHOD%
echo [build] project : %PROJECT%
echo [build] log     : %LOG%
echo [build] A cold Unity start takes about 10 minutes. Leave it alone.

"%UNITY_EXE%" -batchmode -quit -projectPath "%PROJECT%" -logFile "%LOG%" -executeMethod %METHOD%
set CODE=%ERRORLEVEL%

echo.
echo [build] ---- compile errors (empty is good) ----
findstr /C:"error CS" "%LOG%" 2>nul
echo [build] ---- builder output ----
findstr /C:"[Build]" "%LOG%" 2>nul

echo.
if %CODE%==0 (
  echo [build] OK. Launch it with play-game.bat
) else (
  echo [build] FAILED with exit code %CODE%. See %LOG%
)
exit /b %CODE%
