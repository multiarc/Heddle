using System.IO;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;

namespace Heddle.Extensions {
    [ExtensionName("import")]
    public class ImportExtension:AbstractExtension {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            base.InitStart(initContext, dataType, chainedType, parent);
            var nullScope = Scope.Null;
            initContext.ParameterTemplate = GetInnerResult(nullScope);
            var compileContext = initContext.CompileScope.CompileContext;
            string resolvedPath = Path.Combine(initContext.CompileScope.Options.RootPath, initContext.ParameterTemplate);

            // Phase 6 D25 (stamp site 2): mark the @import() parse's diagnostics with a shared origin at the
            // extension's site so the LSP facade re-anchors them. Flag-gated; no definition merge on this path.
            bool markProvenance = initContext.CompileScope.Options.ProvideLanguageFeatures;
            ImportOrigin origin = null;
            int ceMark = 0, cwMark = 0;
            if (markProvenance)
            {
                origin = new ImportOrigin(resolvedPath, Position);
                ceMark = compileContext.CompileErrors.Count;
                cwMark = compileContext.CompileWarnings.Count;
                if (initContext.ParseContext.ImportOrigin == null)
                    initContext.ParseContext.ImportOrigin = origin;
            }

            using (var file = File.OpenText(resolvedPath))
            {
                var document = file.ReadToEnd();
                DocumentParser.Parse(document, initContext.ParseContext, compileContext/*, true*/);
            }

            if (markProvenance)
            {
                StampImportOrigin(compileContext.CompileErrors, ceMark, origin);
                StampImportOrigin(compileContext.CompileWarnings, cwMark, origin);
            }
            return null;
        }

        private static void StampImportOrigin<T>(System.Collections.Generic.List<T> list, int mark, ImportOrigin origin)
            where T : HeddleCompileError
        {
            for (int i = mark; i < list.Count; i++)
            {
                if (list[i].ImportOrigin == null)
                    list[i].ImportOrigin = origin;
            }
        }

        public override object ProcessData(in Scope scope)
        {
            return null;
        }

        public override void RenderData(in Scope scope)
        {
        }
    }
}
