using System;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Runtime;

namespace Heddle.Core
{
    /// <summary>
    /// The one-shot seam that lets a child-template-hosting hook run for real without compiling its child off
    /// disk.
    /// <para><c>HeddleTemplate.Compile(CompileContext)</c> is the engine's only door to a <b>named</b> template
    /// compile, so it is to a child template what <c>AbstractExtension.InitSubTemplate</c> is to a body. Arming
    /// the supply closes that door for the hooks one call site runs: <c>PartialExtension</c>'s name evaluation, its
    /// delayed-queue scheduling, its <c>ImportOrigin</c> marking and its <c>InnerTemplate</c> lifetime all run
    /// unchanged, and where the engine would have read the child off disk and compiled it, a strategy that binds
    /// the child the precompiled way is installed instead.</para>
    /// <para><b>Why the binding is not done here and now.</b> Two things stop it, both structural. The registry
    /// snapshot a static initializer can see never contains the siblings of the assembly being registered:
    /// <c>PrecompiledTemplates.Register</c> materialises every entry's strategy — which runs every generated
    /// type's initializer — before it publishes the snapshot, so a parent asking for its own assembly's child at
    /// type-init would always miss. And the dynamic arm a miss falls to needs the <b>request's</b>
    /// <c>RootPath</c>/<c>FileNamePostfix</c>, which is a host deployment fact no build can carry. So the seam
    /// supplies a strategy that binds once, on first use, and everything else about the hook is decided at static
    /// init.</para>
    /// <para>Thread-static, so a concurrent dynamic compile on another thread never sees an armed supply. The
    /// dynamic path pays one thread-static read per named compile, which is compile time only — nothing here is
    /// on the render path.</para>
    /// </summary>
    internal static class PrecompiledChildSupply
    {
        // Armed state is a flag rather than a prebuilt artifact: unlike a body, which the build emits and hands
        // over, a child template is named by the hook at init and supplied on its own terms each time it is asked
        // for. There is nothing per-frame to carry.
        [System.ThreadStatic] private static bool _armed;

        /// <summary>Arms the supply for the child compiles of one call site on this thread. The caller must
        /// <see cref="Disarm"/> in a <c>finally</c>.</summary>
        internal static void Arm() => _armed = true;

        /// <summary>Restores the previous state. Arming is never nested, so this clears rather than pops.</summary>
        internal static void Disarm() => _armed = false;

        /// <summary>
        /// Supplies the armed child in place of a compile. Returns false when nothing is armed, which is every
        /// dynamic-tier compile.
        /// </summary>
        /// <param name="context">The compile context the hook built for its child — the child's name and the model
        /// type the hook chose for it.</param>
        /// <param name="strategy">The strategy to install as the child template's body.</param>
        internal static bool TryConsume(CompileContext context, out IProcessStrategy strategy)
        {
            if (!_armed || context == null)
            {
                strategy = null;
                return false;
            }

            // The model type travels as the engine's own ExType, dynamic flag included: collapsing it to a Type
            // makes ExType.Dynamic indistinguishable from a real System.Object and compiles the child against a
            // model that admits no member read.
            strategy = new PrecompiledChildStrategy(context.Options?.TemplateName, context.ScopeType);
            return true;
        }

        /// <summary>
        /// A child template a precompiled parent hosts: the registry entry for the name the hook computed, or —
        /// on a miss — the engine's own dynamic compile of it under the options the render request carries.
        /// Bound once and memoized; every render after the first is a volatile read and a virtual call.
        /// </summary>
        private sealed class PrecompiledChildStrategy : IProcessStrategy
        {
            private readonly string _name;
            private readonly ExType _modelType;
            private volatile IProcessStrategy _registryChild;
            private volatile HeddleTemplate _dynamicChild;
            private readonly object _bindGate = new object();

            internal PrecompiledChildStrategy(string name, ExType modelType)
            {
                _name = name ?? string.Empty;
                _modelType = modelType;
            }

            public string Execute(in Scope scope)
            {
                var registry = Bind();
                if (registry != null)
                    return registry.Execute(scope);
                return _dynamicChild.Generate(scope.ModelData, scope.ChainedData) ?? string.Empty;
            }

            public void Render(in Scope scope)
            {
                var registry = Bind();
                if (registry != null)
                {
                    registry.Render(scope);
                    return;
                }

                // Streamed through the caller's renderer, exactly as PartialExtension.RenderData streams a
                // dynamically compiled InnerTemplate. Nothing materialises the child's output as a string.
                _dynamicChild.Render(scope.ModelData, scope.ChainedData, null, scope.Renderer);
            }

            /// <summary>The registry entry for this child, or <c>null</c> once the dynamic child is bound.</summary>
            private IProcessStrategy Bind()
            {
                var registry = _registryChild;
                if (registry != null)
                    return registry;
                if (_dynamicChild != null)
                    return null;

                lock (_bindGate)
                {
                    if (_registryChild != null)
                        return _registryChild;
                    if (_dynamicChild != null)
                        return null;

                    if (PrecompiledTemplates.TryGet(_name, out var entry) && entry.IsPrecompiled)
                    {
                        _registryChild = entry.Strategy;
                        return _registryChild;
                    }

                    var ambient = PrecompiledRuntime.AmbientOptions ?? new TemplateOptions();
                    var childOptions = new TemplateOptions(ambient, TemplateKey.StripTemplateExtension(_name));
                    var template = new HeddleTemplate(new CompileContext(childOptions,
                        _modelType ?? ExType.Dynamic));
                    if (!template.CompileResult.Success)
                        throw new Heddle.Exceptions.TemplateCompileException(template.CompileResult.ErrorList);
                    _dynamicChild = template;
                    return null;
                }
            }
        }
    }
}
