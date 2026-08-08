@echo off
:: Kuncheng RPG - Windows Launch Script
:: Unsets ELECTRON_RUN_AS_NODE which interferes with Electron's API
set ELECTRON_RUN_AS_NODE=
:: Ensure data directory exists for localStorage/cache
if not exist "data" mkdir "data"
:: Launch the game with dev tools
node_modules\electron\dist\electron.exe . %*
