using Xunit;

// TemplateEmitter.FaultInjector is a process-global mutable static in production generator code, consulted on every
// emit. Tests that set it are safe only while nothing else is emitting concurrently, and the other suites in this
// repo already serialise for the same class of reason.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
