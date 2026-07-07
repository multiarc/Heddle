// Phase 9 D3 — the plain .NET WebAssembly browser app entry point. No UI, no routing: the runtime boots this
// assembly, then JS resolves getAssemblyExports and drives DemoInterop's [JSExport] surface from the module worker.
// The C# tier is trimmed out via the Heddle.CSharpTierEnabled feature switch (csproj), so this bundle ships no Roslyn.

System.Console.WriteLine("Heddle demo host booted.");
