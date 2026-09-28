import { dotnet } from './_framework/dotnet.js'

const status = document.getElementById('status');
const canvas = document.getElementById('canvas');

const { getAssemblyExports, getConfig, setModuleImports } = await dotnet
    .withDiagnosticTracing(false)
    .create();

setModuleImports('main.js', {
    setStatus: (text) => { status.textContent = text; },
});

const exports = await getAssemblyExports(getConfig().mainAssemblyName);
const app = exports.Sandbox.Web.WebApp;

const params = new URLSearchParams(location.search);
const scene = params.get('scene') ?? 'circlingsquares';
const ok = app.Initialize('#canvas', canvas.width, canvas.height, scene);
if (!ok) {
    status.textContent = 'Failed to initialize Impeller (see console).';
    console.log('NIMPELLER_SMOKE_FAIL initialize');
} else if (params.has('smoke')) {
    smoke();
} else {
    const frame = (t) => {
        app.RenderFrame(t);
        requestAnimationFrame(frame);
    };
    requestAnimationFrame(frame);
}

// ?smoke: startup checks, then one frame of each scene. Read back in the same task, before the
// WebGL drawing buffer is cleared. test/smoke.mjs waits for the NIMPELLER_SMOKE_* line.
async function smoke() {
    const failures = [];
    if (!app.RunStartupChecks()) {
        failures.push('startup checks');
    }
    const copy = document.createElement('canvas');
    copy.width = canvas.width;
    copy.height = canvas.height;
    const ctx = copy.getContext('2d', { willReadFrequently: true });
    window.nimpellerSmoke = {};
    for (const name of app.SceneNames()) {
        app.SetScene(name);
        await new Promise(requestAnimationFrame);
        app.RenderFrame(performance.now());
        ctx.drawImage(canvas, 0, 0);
        const pixels = ctx.getImageData(0, 0, copy.width, copy.height).data;
        const colors = new Set();
        for (let i = 0; i < pixels.length && colors.size < 1000; i += 4 * 7) {
            colors.add((pixels[i] << 16) | (pixels[i + 1] << 8) | pixels[i + 2]);
        }
        window.nimpellerSmoke[name] = copy.toDataURL('image/png');
        console.log(`NIMPELLER_SMOKE_SCENE ${name} ${colors.size} colors`);
        if (colors.size < 2) {
            failures.push(`${name} is blank`);
        }
    }
    console.log(failures.length ? `NIMPELLER_SMOKE_FAIL ${failures.join(', ')}` : 'NIMPELLER_SMOKE_OK');
}
