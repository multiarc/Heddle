// Phase 9 D6 — the module-worker dispatcher. The Microsoft ".NET on Web Workers" pattern verbatim: boot
// dotnet.js, resolve the assembly exports, and forward { id, cmd, ...args } envelopes to DemoInterop's [JSExport]
// surface, replying { id, ok, result | error }. No logic lives here — DemoHost owns all behavior.
import { dotnet } from './_framework/dotnet.js';

let exports = null;
try {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  exports = await getAssemblyExports(getConfig().mainAssemblyName);
  self.postMessage({ evt: 'ready' });
} catch (err) {
  self.postMessage({ evt: 'boot-failed', error: String((err && err.message) || err) });
}

const api = {
  init:     ()  => exports.Heddle.Demo.Wasm.DemoInterop.Init(),
  analyze:  (m) => exports.Heddle.Demo.Wasm.DemoInterop.Analyze(m.path, m.text, m.version),
  complete: (m) => exports.Heddle.Demo.Wasm.DemoInterop.Complete(m.path, m.offset),
  hover:    (m) => exports.Heddle.Demo.Wasm.DemoInterop.Hover(m.path, m.offset),
  render:   (m) => exports.Heddle.Demo.Wasm.DemoInterop.Render(m.path, m.modelId)
};

self.addEventListener('message', (e) => {
  const { id, cmd } = e.data;
  try {
    if (!exports) throw new Error('worker not booted');
    self.postMessage({ id, ok: true, result: JSON.parse(api[cmd](e.data)) });
  } catch (err) {
    self.postMessage({ id, ok: false, error: { message: String((err && err.message) || err) } });
  }
});
