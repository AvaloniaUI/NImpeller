// Headless browser smoke test for a published Sandbox.Web.
//
//   node samples/Sandbox.Web/test/smoke.mjs <publish>/wwwroot [--browser chromium|firefox|all] [--out <dir>] [--headed]
//
// Headless Firefox may have no WebGL; --headed under xvfb-run gives it one.
// Serves the publish output, opens ?smoke, waits for NIMPELLER_SMOKE_OK/_FAIL (main.js prints it after
// the startup checks and one frame of each scene) and saves a PNG per scene. Exits non-zero on failure
// or timeout. Playwright is resolved from node_modules, or from $NIMPELLER_PLAYWRIGHT_DIR
// (default ~/.cache/nimpeller-playwright).
import { createServer } from 'node:http';
import { createRequire } from 'node:module';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

const args = process.argv.slice(2);
const option = (name, fallback) => {
    const i = args.indexOf(name);
    return i >= 0 ? args.splice(i, 2)[1] : fallback;
};
const headed = args.includes('--headed') && args.splice(args.indexOf('--headed'), 1).length > 0;
const browserName = option('--browser', 'all');
const outDir = path.resolve(option('--out', 'smoke-results'));
const root = path.resolve(args[0] ?? '');
const timeoutMs = 180_000;

let playwright;
try {
    playwright = await import('playwright');
} catch {
    const dir = process.env.NIMPELLER_PLAYWRIGHT_DIR ?? path.join(os.homedir(), '.cache', 'nimpeller-playwright');
    playwright = createRequire(path.join(dir, 'package.json'))('playwright');
}

const types = {
    '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.json': 'application/json',
    '.wasm': 'application/wasm', '.css': 'text/css', '.png': 'image/png', '.ico': 'image/x-icon',
};
const server = createServer(async (req, res) => {
    const file = path.join(root, decodeURIComponent(new URL(req.url, 'http://x').pathname));
    if (!file.startsWith(root)) {
        res.writeHead(403).end();
        return;
    }
    try {
        const body = await readFile(file.endsWith(path.sep) ? path.join(file, 'index.html') : file);
        res.writeHead(200, { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream' }).end(body);
    } catch {
        res.writeHead(404).end();
    }
});
await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
const url = `http://127.0.0.1:${server.address().port}/index.html?smoke`;

const browsers = {
    chromium: () => playwright.chromium.launch({ headless: !headed, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] }),
    firefox: () => playwright.firefox.launch({ headless: !headed, firefoxUserPrefs: { 'webgl.force-enabled': true } }),
};

async function run(name) {
    const browser = await browsers[name]();
    try {
        const page = await browser.newPage({ viewport: { width: 900, height: 720 } });
        const result = new Promise(resolve => {
            page.on('console', msg => {
                const text = msg.text();
                console.log(`[${name}] ${text}`);
                if (text.startsWith('NIMPELLER_SMOKE_OK') || text.startsWith('NIMPELLER_SMOKE_FAIL')) {
                    resolve(text);
                }
            });
            page.on('pageerror', err => resolve(`NIMPELLER_SMOKE_FAIL page error: ${err.message}`));
            setTimeout(() => resolve('NIMPELLER_SMOKE_FAIL timeout'), timeoutMs);
        });
        await page.goto(url);
        const verdict = await result;

        const shots = await page.evaluate(() => window.nimpellerSmoke ?? {});
        for (const [scene, dataUrl] of Object.entries(shots)) {
            await writeFile(path.join(outDir, `${name}-${scene}.png`), Buffer.from(dataUrl.split(',')[1], 'base64'));
        }
        return verdict.startsWith('NIMPELLER_SMOKE_OK');
    } finally {
        await browser.close();
    }
}

await mkdir(outDir, { recursive: true });
let ok = true;
for (const name of browserName === 'all' ? Object.keys(browsers) : [browserName]) {
    const passed = await run(name);
    console.log(`${name}: ${passed ? 'OK' : 'FAILED'}`);
    ok &&= passed;
}
server.close();
console.log(`Screenshots: ${outDir}`);
process.exit(ok ? 0 : 1);
