@echo off
:: Kuncheng RPG - launch the built Windows player.
:: ASCII only: see the note in build-game.bat.
setlocal

set EXE=%~dp0Build\Windows\KunchengRPG.exe

if not exist "%EXE%" (
  echo [play] No player build found at:
  echo [play]   %EXE%
  echo.
  echo [play] Build it first:
  echo [play]   build-game.bat            ^(dev build, keeps the log window^)
  echo [play]   build-game.bat release    ^(release build^)
  echo.
  echo [play] Or just press Play in the Unity editor on Assets/Scenes/TitleScene.unity
  exit /b 1
)

echo [play] launching %EXE%
start "" "%EXE%" %*
