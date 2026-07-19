using Xunit;

// Heddle uses process-global static state (TemplateFactory, AssemblyHelper, CSharpContext caches). Running
// test classes in parallel races those singletons — e.g. HeddleTemplate.Configure mutating the shared
// assembly list while a C#-tier test compiles through it. Serialize the suite to keep it deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
