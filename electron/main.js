const { app, BrowserWindow } = require('electron');
const path = require('path');
const fs = require('fs');

const isDev = process.argv.includes('--dev');

// Disable hardware acceleration for compatibility with VMs/remote desktops
app.disableHardwareAcceleration();
// Disable sandbox for environments where it causes issues
app.commandLine.appendSwitch('no-sandbox');

// Set userData to a writable local directory to fix cache/localStorage permission issues
const userDataPath = path.join(__dirname, '..', 'data');
try {
    if (!fs.existsSync(userDataPath)) {
        fs.mkdirSync(userDataPath, { recursive: true });
    }
    app.setPath('userData', userDataPath);
} catch (e) {
    console.error('Failed to set userData path:', e.message);
}

let mainWindow = null;

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1280,
        height: 800,
        minWidth: 960,
        minHeight: 600,
        title: '鲲城 RPG',
        backgroundColor: '#1a1a2e',
        show: false,
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            contextIsolation: true,
            nodeIntegration: false
        }
    });

    mainWindow.loadFile(path.join(__dirname, '..', 'index.html'));

    // Capture renderer console for debugging
    mainWindow.webContents.on('console-message', (event, level, message, line, sourceId) => {
        const levels = ['LOG', 'WARN', 'ERROR'];
        console.log(`[R:${levels[level] || level}] ${message}`);
    });

    if (isDev) {
        mainWindow.webContents.openDevTools();
    }

    mainWindow.once('ready-to-show', () => {
        mainWindow.show();
    });

    mainWindow.on('closed', () => {
        mainWindow = null;
    });
}

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
        createWindow();
    }
});
