/**
 * Boot smoke check: loads the game in an offscreen Electron window, collects
 * renderer errors, and reports whether the title scene actually came up.
 *
 * Run against the preview server (see preview.bat):
 *   node_modules\electron\dist\electron.exe tools\smoke-check.js
 *   node_modules\electron\dist\electron.exe tools\smoke-check.js http://localhost:5174/
 *
 * Result is printed to stdout and written to data/smoke-result.json, because
 * electron.exe is a GUI-subsystem binary and stdout is often not visible.
 */
const { app, BrowserWindow } = require('electron');
const path = require('path');
const fs = require('fs');

const TARGET = process.argv.find(a => a.startsWith('http')) || 'http://localhost:5173/';
const SETTLE_MS = 5000;
const OUT_FILE = path.join(__dirname, '..', 'data', 'smoke-result.json');

app.disableHardwareAcceleration();
app.commandLine.appendSwitch('no-sandbox');

const errors = [];
const logs = [];

function finish(result) {
    const body = JSON.stringify(result, null, 2);
    try {
        fs.mkdirSync(path.dirname(OUT_FILE), { recursive: true });
        fs.writeFileSync(OUT_FILE, body);
    } catch (e) {
        // stdout still carries the result
    }
    process.stdout.write(body + '\n');
    app.exit(result.ok ? 0 : 1);
}

app.whenReady().then(() => {
    const win = new BrowserWindow({
        width: 1280,
        height: 800,
        show: false,
        webPreferences: {
            preload: path.join(__dirname, '..', 'electron', 'preload.js'),
            contextIsolation: true,
            nodeIntegration: false,
            offscreen: true
        }
    });

    win.webContents.on('console-message', (event, level, message, line, sourceId) => {
        if (/Electron Security Warning/.test(message)) return;
        const where = sourceId ? ` (${sourceId}:${line})` : '';
        if (level >= 2) {
            errors.push(message + where);
        } else {
            logs.push(message + where);
        }
    });

    win.webContents.on('did-fail-load', (event, code, desc, url) => {
        finish({ ok: false, target: TARGET, stage: 'did-fail-load', code, desc, url });
    });

    win.webContents.on('render-process-gone', (event, details) => {
        finish({ ok: false, target: TARGET, stage: 'render-process-gone', details });
    });

    win.loadURL(TARGET).then(() => {
        setTimeout(async () => {
            let probe;
            try {
                probe = await win.webContents.executeJavaScript(`(() => {
                    const c = document.getElementById('gameCanvas');
                    const ls = document.getElementById('loadingScreen');
                    const lt = document.getElementById('loadingText');
                    return {
                        canvasWidth: c ? c.width : 0,
                        canvasHeight: c ? c.height : 0,
                        loadingHidden: ls ? ls.classList.contains('hidden') : false,
                        loadingText: lt ? lt.textContent : null,
                        canvasBlank: (() => {
                            if (!c) return true;
                            const ctx = c.getContext('2d');
                            if (!ctx || !c.width || !c.height) return true;
                            const d = ctx.getImageData(0, 0, c.width, c.height).data;
                            const first = [d[0], d[1], d[2]];
                            for (let i = 4; i < d.length; i += 4 * 97) {
                                if (d[i] !== first[0] || d[i+1] !== first[1] || d[i+2] !== first[2]) return false;
                            }
                            return true;
                        })()
                    };
                })()`);
            } catch (e) {
                finish({ ok: false, target: TARGET, stage: 'probe-failed', error: e.message, errors });
                return;
            }

            const problems = [];
            if (errors.length) problems.push('renderer errors: ' + errors.length);
            if (!probe.loadingHidden) problems.push('loading screen never hid');
            if (!probe.canvasWidth || !probe.canvasHeight) problems.push('canvas has zero size');
            if (probe.canvasBlank) problems.push('canvas rendered nothing');
            if (probe.loadingText && /ERROR/i.test(probe.loadingText)) {
                problems.push('loading text: ' + probe.loadingText);
            }

            finish({
                ok: problems.length === 0,
                target: TARGET,
                problems,
                probe,
                errors,
                logTail: logs.slice(-12)
            });
        }, SETTLE_MS);
    }).catch(e => {
        finish({ ok: false, target: TARGET, stage: 'loadURL-rejected', error: e.message });
    });
});
