using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;

namespace Heddle.Extensions {
    /// <summary>
    /// <para>Partial Template</para>
    /// <para>Optional parameter is sub-template (fully inclusive)</para>
    /// </summary>
    [ExtensionName ("partial")]
    public class PartialExtension: AbstractExtension
    {
        protected HeddleTemplate InnerTemplate;

        public override object ProcessData (in Scope scope)
        {
            return InnerTemplate?.Generate(scope.ModelData, scope.ChainedData);
        }

        public override void RenderData(in Scope scope)
        {
            // Phase 8 D11: stream through the caller's renderer instead of materializing the partial's whole output as
            // a string. The null third argument is callerData — the pre-phase two-argument Generate call also passed
            // none, so this is argument-for-argument identical, and byte-identity is pinned by the golden corpus.
            InnerTemplate?.Render(scope.ModelData, scope.ChainedData, null, scope.Renderer);
        }

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            if (initContext.CompileScope == null)
                throw new ArgumentNullException(nameof(initContext.CompileScope));
            base.InitStart(initContext, dataType, chainedType, parent);
            var nullScope = Scope.Null;
            initContext.ParameterTemplate = GetInnerResult(nullScope);
            if (!string.IsNullOrEmpty(initContext.ParameterTemplate)) {
                string templateName = initContext.ParameterTemplate.Trim();
                if (!string.IsNullOrEmpty(templateName))
                {
                    initContext.CompileScope.CompileContext.AddDelayedCompileTemplate(
                        new CompileScope(new CompileContext(initContext.CompileScope.CompileContext, dataType, templateName),
                            initContext.CompileScope.CSharpContext), initContext.ParseContext, this);
                }
            }
            return typeof (string);
        }

        public override void CompleteInit(CompileScope newScope, ParseContext parseContext)
        {
            InnerTemplate = new HeddleTemplate();

            // Phase 6 D25 (stamp site 3): partials AddRange the child compile's errors the same coordinate-foreign
            // way imports do, so mark them with a shared origin at the @partial site for facade re-anchoring.
            bool markProvenance = newScope.Options.ProvideLanguageFeatures;
            int ceMark = markProvenance ? newScope.CompileErrors.Count : 0;

            var result = InnerTemplate.Compile(newScope.CompileContext);
            if (!result.Success)
            {
                newScope.CompileErrors.AddRange(result.Errors);
            }

            if (markProvenance)
            {
                var origin = new ImportOrigin(newScope.Options.FullPath, Position);
                var errors = newScope.CompileErrors;
                for (int i = ceMark; i < errors.Count; i++)
                {
                    if (errors[i].ImportOrigin == null)
                        errors[i].ImportOrigin = origin;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            InnerTemplate?.Dispose();
        }
    }
}