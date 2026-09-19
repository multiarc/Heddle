using Heddle.Data;
using Heddle.Runtime;

namespace Heddle.Language
{
    /// <summary>Runtime <c>CompileContext</c> adapters over <see cref="DocumentParser"/>.</summary>
    public static partial class DocumentParser
    {
        /// <summary>Parse document and copy diagnostics to compile context.</summary>
        /// <returns>Parse context tree</returns>
        public static ParseContext Parse(string document, CompileContext compileContext, out string cleanDocument)
        {
            if (compileContext == null)
                throw new System.ArgumentNullException(nameof(compileContext));
            var settings = SettingsFrom(compileContext);
            var context = new ParseContext(provideLanguageFeatures: settings.ProvideLanguageFeatures);
            cleanDocument = Parse(document, context, settings);
            CopyErrorsTo(context, compileContext, errorFrom: 0);
            return context;
        }

        private static ParserSettings SettingsFrom(CompileContext compileContext)
        {
            var options = compileContext.Options;
            return new ParserSettings
            {
                RootPath = options.RootPath,
                ProvideLanguageFeatures = options.ProvideLanguageFeatures,
                ImportReader = compileContext.ImportReader,
                ImportIdentifier = compileContext.ImportIdentifier
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
