@echo off
:: Kuncheng RPG - browser preview server
:: Uses the Node runtime bundled in the project's Electron binary,
:: so no separate Node/npm install is required.
setlocal
cd /d "%~dp0"

if not exist "node_modules\electron\dist\electron.exe" (
    echo [ERROR] node_modules\electron\dist\electron.exe not found.
    echo         Restore dependencies before running preview.
    exit /b 1
)

set ELECTRON_RUN_AS_NODE=1
echo Starting local preview server...
node_modules\electron\dist\electron.exe tools\dev-server.js %*
endlocal
