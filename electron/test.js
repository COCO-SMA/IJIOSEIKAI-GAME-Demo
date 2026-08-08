const electron = require('electron');
console.log('typeof electron:', typeof electron);
console.log('keys:', Object.keys(electron || {}));
console.log('app:', typeof electron.app);
console.log('BrowserWindow:', typeof electron.BrowserWindow);
if (electron.app) {
    electron.app.whenReady().then(() => {
        console.log('Electron app is ready!');
        const win = new electron.BrowserWindow({ width: 400, height: 300 });
        win.loadURL('data:text/html,<h1 style="color:white;background:black;padding:50px;text-align:center">Electron works!</h1>');
        setTimeout(() => electron.app.quit(), 3000);
    });
} else {
    console.error('FATAL: electron.app is undefined!');
    process.exit(1);
}
