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

const scene = new URLSearchParams(location.search).get('scene') ?? 'circlingsquares';
const ok = app.Initialize('#canvas', canvas.width, canvas.height, scene);
if (!ok) {
    status.textContent = 'Failed to initialize Impeller (see console).';
} else {
    const frame = (t) => {
        app.RenderFrame(t);
        requestAnimationFrame(frame);
    };
    requestAnimationFrame(frame);
}
