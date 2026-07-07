using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Language
{
    /// <summary>
    /// The runtime <c>CompileContext</c> adapters over the shared <see cref="DocumentParser"/> core (phase 7 D4).
    /// Kept in a separate partial file so the generator's shared-source compile (which has no <c>CompileContext</c>)
    /// excludes this file. Each adapter builds a <see cref="ParserSettings"/> from the compile context's options,
    /// runs the shared core, then copies the front-end diagnostics collected on the <see cref="ParseContext"/> into
    /// the compile context — the single copy point the D4 seam funnels all front-end errors through.
    /// </summary>
    public static partial class DocumentParser
    {
        /// <summary>Performs parse of document (runtime adapter).</summary>
        /// <returns>Full template context tree found in source template</returns>
        public static ParseContext Parse(string document, CompileContext compileContext, out string cleanDocument)
        {
            if (compileContext == null)
                throw new System.ArgumentNullException(nameof(compileContext));
            var settings = SettingsFrom(compileContext.Options);
            var context = new ParseContext(provideLanguageFeatures: settings.ProvideLanguageFeatures);
            cleanDocument = Parse(document, context, settings);
            CopyErrorsTo(context, compileContext, errorFrom: 0);
            return context;
        }

        /// <summary>
        /// Runtime adapter over an existing <see cref="ParseContext"/> (used by the compile-time <c>@import()</c>
        /// path). Copies the diagnostics this call adds to the context into the compile context, preserving the
        /// pre-seam behavior where those errors landed directly in <see cref="CompileContext.CompileErrors"/>.
        /// </summary>
        public static string Parse(string document, ParseContext context, CompileContext compileContext)
        {
            if (context == null)
                throw new System.ArgumentNullException(nameof(context));
            if (compileContext == null)
                throw new System.ArgumentNullException(nameof(compileContext));
            var settings = SettingsFrom(compileContext.Options);
            var errorFrom = context.Errors.Count;
            var cleanDocument = Parse(document, context, settings);
            CopyErrorsTo(context, compileContext, errorFrom);
            return cleanDocument;
        }

        private static ParserSettings SettingsFrom(TemplateOptions options)
        {
            return new ParserSettings
            {
                RootPath = options.RootPath,
                ProvideLanguageFeatures = options.ProvideLanguageFeatures,
                ImportReader = null
            };
        }

        private static void CopyErrorsTo(ParseContext context, CompileContext compileContext, int errorFrom)
        {
            var errors = context.Errors;
            for (var i = errorFrom; i < errors.Count; i++)
                compileContext.CompileErrors.Add(errors[i]);
        }
    }
}
