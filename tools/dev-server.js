/**
 * Zero-dependency static dev server for browser preview.
 *
 * The machine has no standalone Node/npm, so this is launched with the Node
 * runtime bundled inside the project's Electron binary:
 *   set ELECTRON_RUN_AS_NODE=1
 *   node_modules\electron\dist\electron.exe tools\dev-server.js
 *
 * Binds to 127.0.0.1 only (never exposed on the LAN) and refuses any path
 * that escapes the project root.
 */
const http = require('http');
const fs = require('fs');
const path = require('path');
const url = require('url');

const ROOT = path.resolve(__dirname, '..');
const START_PORT = Number(process.env.PORT || process.argv[2] || 5173);
const MAX_PORT_TRIES = 20;

const MIME = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.mjs': 'text/javascript; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.png': 'image/png',
    '.jpg': 'image/jpeg',
    '.jpeg': 'image/jpeg',
    '.gif': 'image/gif',
    '.webp': 'image/webp',
    '.svg': 'image/svg+xml',
    '.ico': 'image/x-icon',
    '.ogg': 'audio/ogg',
    '.mp3': 'audio/mpeg',
    '.wav': 'audio/wav',
    '.woff': 'font/woff',
    '.woff2': 'font/woff2',
    '.ttf': 'font/ttf',
    '.map': 'application/json; charset=utf-8'
};

// Directories that must never be served, even though they live under ROOT.
const BLOCKED = ['node_modules', 'data', '.git', '.github', '.workbuddy', 'dist'];

function log(line) {
    process.stdout.write(line + '\n');
}

function isBlocked(relPath) {
    const first = relPath.split(path.sep)[0].toLowerCase();
    return BLOCKED.includes(first);
}

function send(res, status, body, headers) {
    res.writeHead(status, Object.assign({
        'Cache-Control': 'no-store, must-revalidate',
        'Content-Length': Buffer.byteLength(body)
    }, headers || {}));
    res.end(body);
}

function serveFile(res, filePath) {
    fs.readFile(filePath, (err, buf) => {
        if (err) {
            send(res, 500, 'Read error: ' + err.code, { 'Content-Type': 'text/plain; charset=utf-8' });
            return;
        }
        const type = MIME[path.extname(filePath).toLowerCase()] || 'application/octet-stream';
        res.writeHead(200, {
            'Content-Type': type,
            'Content-Length': buf.length,
            'Cache-Control': 'no-store, must-revalidate'
        });
        res.end(buf);
    });
}

const server = http.createServer((req, res) => {
    if (req.method !== 'GET' && req.method !== 'HEAD') {
        send(res, 405, 'Method Not Allowed', { 'Content-Type': 'text/plain; charset=utf-8' });
        return;
    }

    let pathname;
    try {
        pathname = decodeURIComponent(url.parse(req.url).pathname || '/');
    } catch (e) {
        send(res, 400, 'Bad request path', { 'Content-Type': 'text/plain; charset=utf-8' });
        return;
    }

    if (pathname === '/' || pathname.endsWith('/')) {
        pathname += 'index.html';
    }

    const target = path.resolve(ROOT, '.' + pathname.replace(/\//g, path.sep));
    const rel = path.relative(ROOT, target);

    if (rel.startsWith('..') || path.isAbsolute(rel) || isBlocked(rel)) {
        log(`  403 ${pathname}`);
        send(res, 403, 'Forbidden', { 'Content-Type': 'text/plain; charset=utf-8' });
        return;
    }

    fs.stat(target, (err, stat) => {
        if (err || !stat.isFile()) {
            log(`  404 ${pathname}`);
            send(res, 404, 'Not found: ' + pathname, { 'Content-Type': 'text/plain; charset=utf-8' });
            return;
        }
        log(`  200 ${pathname}`);
        serveFile(res, target);
    });
});

let port = START_PORT;
let tries = 0;

server.on('error', (err) => {
    if (err.code === 'EADDRINUSE' && tries < MAX_PORT_TRIES) {
        tries++;
        port++;
        server.listen(port, '127.0.0.1');
        return;
    }
    log('Server error: ' + err.message);
    process.exit(1);
});

server.on('listening', () => {
    log('');
    log('  Kuncheng RPG - local preview');
    log('  root:  ' + ROOT);
    log('  url:   http://localhost:' + port + '/');
    log('');
    log('  Ctrl+C to stop.');
    log('');
});

server.listen(port, '127.0.0.1');
