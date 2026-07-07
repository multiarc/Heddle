// The WebAssembly SDK requires a main JS entry. The Heddle demo does NOT boot on the main thread — the demo page
// (docs/public/demo.html) creates a module worker from heddle-demo-worker.js instead (phase 9 D6), so this file is
// only a fallback for opening the bundle standalone. It boots the runtime and logs that the exports are reachable.
import { dotnet } from './_framework/dotnet.js';

try {
  const { getAssemblyExports, getConfig, runMain } = await dotnet.create();
  const exports = await getAssemblyExports(getConfig().mainAssemblyName);
  await runMain();
  // Reachable in a standalone open; the real UI drives DemoInterop through the worker.
  console.log('Heddle demo exports ready:', Object.keys(exports.Heddle.Demo.Wasm.DemoInterop));
} catch (err) {
  console.error('Heddle demo boot failed', err);
}
