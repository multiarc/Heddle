using System.Runtime.CompilerServices;
using Heddle;
using Xunit;

// The differential harness drives process-global engine state (TemplateFactory, the precompiled registry).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

// The engine loads and scans nothing on its own, so this assembly's [ExportExtensions] fixtures reach the registry
// only by registering it — before any test renders, hence the module initializer.
internal static class ExtensionRegistration
{
    [ModuleInitializer]
    internal static void Register()
    {
        HeddleTemplate.Register(typeof(ExtensionRegistration).Assembly);
    }
}
