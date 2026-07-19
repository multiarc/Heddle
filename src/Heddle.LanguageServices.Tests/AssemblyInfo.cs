using Xunit;

// The facade drives process-global engine state (TemplateFactory, AssemblyHelper). Serialize the suite.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
