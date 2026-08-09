using System;
using System.Collections.Generic;
using Heddle.Core;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Heddle.Strings.Core;

namespace Heddle.Precompiled
{
    /// <summary>
    /// The substitute a faulted <see cref="PrecompiledRuntime.Init"/> returns for one call site: it compiles that
    /// call's own source text as its own document and renders it, so the call costs a tier rather than a byte while
    /// the rest of the template stays precompiled.
    /// <para><b>The compile is lazy, and has to be.</b> A compile needs the ambient
    /// <see cref="TemplateOptions"/>, which exists only during a render — <see cref="PrecompiledRuntime"/>
    /// establishes it at every entry point. Type-init has no request, so nothing is compiled there; the first
    /// render resolves once and memoises, the same "resolve once at first use" shape
    /// <see cref="PrecompiledFunctionSite"/> uses, and a compile that fails is captured and re-raised on every
    /// render rather than thrown out of a type initializer.</para>
    /// <para><b>Bounded claim, stated plainly.</b> A fragment compiled as its own document is not a universal
    /// solvent. It sees no enclosing definitions, so a body calling one gets HED0002; no ambient region fill scope,
    /// so a <c>&lt;:region&gt;</c> fill from the enclosing document does not reach it; and no active prop layout, so
    /// a body reading a prop of the definition it sits in does not resolve. It also carries the enclosing document's
    /// model type but not the enclosing document's <c>@using</c> set beyond what the site records. Stage 5b must
    /// refuse this substitute for a call whose body references any of those and refuse the whole template
    /// instead.</para>
    /// </summary>
    internal sealed class PrecompiledSiteFallbackExtension : AbstractExtension
    {
        private readonly PrecompiledInitSite _site;
        private object _resolved;

        internal PrecompiledSiteFallbackExtension(PrecompiledInitSite site)
        {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            Position = new BlockPosition(site.PositionStart, site.PositionLength);
        }

        public override object ProcessData(in Scope scope)
            => Template().Generate(scope.ModelData, scope.ChainedData);

        public override void RenderData(in Scope scope)
            => Template().Render(scope.ModelData, scope.ChainedData, null, scope.Renderer);

        /// <summary>The memoised document. Racing first renders may each compile; the results are equivalent and
        /// the last write wins, exactly as <see cref="PrecompiledFunctionSite"/>'s bind races.</summary>
        private HeddleTemplate Template()
        {
            var resolved = System.Threading.Volatile.Read(ref _resolved);
            if (resolved == null)
            {
                resolved = Resolve();
                System.Threading.Volatile.Write(ref _resolved, resolved);
            }

            if (resolved is HeddleCompileError[] errors)
                throw new TemplateCompileException(errors);
            return (HeddleTemplate) resolved;
        }

        private object Resolve()
        {
            try
            {
                var template = new HeddleTemplate();
                var result = template.Compile(_site.SourceText ?? string.Empty,
                    new CompileContext(Options(), _site.ModelType == null ? ExType.Dynamic : new ExType(_site.ModelType)));
                if (result.Success)
                    return template;
                return Anchored(ToArray(result.Errors));
            }
            catch (Exception e)
            {
                return new[] { e.ToError(Position, HeddleDiagnosticIds.CompilationFailed) };
            }
        }

        /// <summary>The request's own options when there is a render in flight — what the dynamic tier would have
        /// compiled this call under — and otherwise the options the build recorded on the site.</summary>
        private TemplateOptions Options()
        {
            var ambient = PrecompiledRuntime.AmbientOptions;
            if (ambient != null)
                return new TemplateOptions(ambient);
            return new TemplateOptions
            {
                OutputProfile = _site.OutputProfile,
                ExpressionMode = _site.ExpressionMode,
                TrimDirectiveLines = _site.TrimDirectiveLines,
                MaxRecursionCount = _site.MaxRecursionCount
            };
        }

        /// <summary>Moves the fragment compile's errors onto the call's own position in the enclosing document.
        /// Their own offsets are into a document that exists nowhere — the fragment is compiled as its own — so
        /// reporting them unmoved points a template author at a coordinate in their file that has nothing to do
        /// with the fault. The call is the smallest span that is genuinely theirs, and it is where the engine
        /// itself reports a fault raised while compiling this item.</summary>
        private HeddleCompileError[] Anchored(HeddleCompileError[] errors)
        {
            for (int i = 0; i < errors.Length; i++)
            {
                if (errors[i] != null)
                    errors[i].Position = Position;
            }

            return errors;
        }

        private static HeddleCompileError[] ToArray(List<HeddleCompileError> errors)
        {
            if (errors == null || errors.Count == 0)
                return new HeddleCompileError[0];
            return errors.ToArray();
        }
    }
}
