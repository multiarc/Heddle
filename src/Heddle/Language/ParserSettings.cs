using System;
using System.Collections.Generic;
using System.IO;

namespace Heddle.Language
{
    /// <summary>
    /// <para>The parse-time configuration seam. Abstracts the two couplings the shared front end
    /// had on the runtime <c>CompileContext</c>: the import root path and the <c>@&lt;&lt;</c> file IO. The runtime
    /// path builds one of these from <see cref="Heddle.Data.TemplateOptions"/> (via the
    /// <see cref="DocumentParser.Parse(string, CompileContext, out string)"/> adapter); the build-time generator
    /// builds one whose <see cref="ImportReader"/> serves import content from <c>AdditionalFiles</c> rather than
    /// disk, keeping generation deterministic and incremental.</para>
    /// <para>Behavior-preserving by construction: a <see cref="ImportReader"/> of <c>null</c> falls back to the
    /// exact <c>File.OpenText(Path.Combine(RootPath, path))</c> read the listener performed inline before the seam
    /// existed.</para>
    /// </summary>
    public sealed class ParserSettings
    {
        /// <summary>The resolver root against which <c>@&lt;&lt;</c> import paths are resolved for file IO.</summary>
        public string RootPath { get; set; } = string.Empty;

        /// <summary>Mirrors <see cref="Heddle.Data.TemplateOptions.ProvideLanguageFeatures"/>: enables editor
        /// token capture, prediction-mode diagnostics, and import provenance stamping.</summary>
        public bool ProvideLanguageFeatures { get; set; }

        /// <summary>
        /// Serves the content of an <c>@&lt;&lt;</c> import. The argument is the import path exactly as written in the
        /// template (resolver-relative). When <c>null</c>, imports are read from disk via
        /// <see cref="ReadImport(string)"/>'s default. A generator supplies a reader that resolves against the
        /// collected import map, records the transitive closure, and never touches the file system.
        /// </summary>
        public Func<string, string> ImportReader { get; set; }

        /// <summary>
        /// The imports currently being parsed, outermost first. An <c>@&lt;&lt;</c> import parses the imported
        /// document in place, so a document that imports its way back to one already on this list would recurse until
        /// the stack ran out — and a <c>StackOverflowException</c> cannot be caught, so a template typo took the whole
        /// process down instead of producing a compile error.
        /// <para>Held here because this object is what threads through the nested parse. Parsing a document is
        /// single-threaded, and every push is unwound in a <c>finally</c>.</para>
        /// </summary>
        internal List<string> ActiveImports { get; } = new List<string>();

        /// <summary>Reads the content of an <c>@&lt;&lt;</c> import, through <see cref="ImportReader"/> when set and
        /// through the default file read otherwise (the pre-seam behavior).</summary>
        internal string ReadImport(string importPath)
        {
            if (ImportReader != null)
                return ImportReader(importPath);

            var resolvedPath = Path.Combine(RootPath ?? string.Empty, importPath);
            using (var file = File.OpenText(resolvedPath))
                return file.ReadToEnd();
        }

        /// <summary>The display/provenance path for an import — <c>Path.Combine(RootPath, importPath)</c>, matching
        /// the pre-seam <c>ImportOrigin.Path</c> value. Only consulted on the language-feature (LSP) path.</summary>
        internal string ResolveImportPath(string importPath)
        {
            return Path.Combine(RootPath ?? string.Empty, importPath);
        }

        /// <summary>
        /// How deep <c>@&lt;&lt;</c> imports may nest. Each one parses the imported document in place, so depth is stack
        /// depth; a chain of five thousand distinct files — no cycle anywhere — exhausted it. The cycle guard catches
        /// repeats, which is a different question from depth.
        /// </summary>
        internal const int MaxImportDepth = 64;

        /// <summary>
        /// How many import cycles one parse will report before it stops describing them. A cycle is reported per
        /// offending edge, and a document reaching the same file under many spellings produced them combinatorially —
        /// eight spellings yielded 863,109 diagnostics at build time. The import is still skipped once the budget is
        /// spent; only the description stops.
        /// </summary>
        internal const int DefaultCycleReportBudget = 32;

        internal int CycleReportBudget { get; set; } = DefaultCycleReportBudget;

        /// <summary>Restores the budget at the start of a top-level parse. Without this the counter was per settings
        /// object rather than per parse, so a host reusing one — the type and the overload taking it are both
        /// public — stopped describing cycles altogether after the thirty-second, while still skipping them.</summary>
        internal void BeginTopLevelParse()
        {
            if (ActiveImports.Count == 0)
                CycleReportBudget = DefaultCycleReportBudget;
        }

        /// <summary>
        /// The identity an <c>@&lt;&lt;</c> import is recognised by when detecting a cycle. Distinct from
        /// <see cref="ResolveImportPath"/>, which is the provenance string shown to a reader and must keep its exact
        /// spelling: <c>a.heddle</c>, <c>./a.heddle</c> and <c>d/../a.heddle</c> are one file, and treating them as
        /// three let a cycle walk straight past the guard.
        /// </summary>
        internal string ImportIdentity(string importPath)
        {
            var combined = ResolveImportPath(importPath);
            try
            {
                return Path.GetFullPath(combined);
            }
            catch (Exception)
            {
                // A root that is not a real path — tests and in-memory readers use one — cannot be canonicalised.
                // The raw spelling is still a usable key; it just cannot see through '..'.
                return combined;
            }
        }
    }
}
