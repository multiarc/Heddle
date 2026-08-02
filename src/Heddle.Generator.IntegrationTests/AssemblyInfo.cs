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

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    // Module initializers are plain IL (<Module>.cctor); only the attribute the compiler looks for is missing
    // from net48's BCL. A referenced package ships one as INTERNAL, which resolves but cannot be used — this
    // local declaration is the accessible one the compiler binds.
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif
