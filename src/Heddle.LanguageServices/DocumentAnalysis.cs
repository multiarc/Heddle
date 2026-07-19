using System.Collections.Generic;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// Immutable snapshot of one analyzed document version (phase 6 D10). Every member is a projection of engine
    /// data; safe to share across threads.
    /// </summary>
    public sealed class DocumentAnalysis
    {
        internal DocumentAnalysis(
            string path, int version, string text, LineMap lines,
            IReadOnlyList<HeddleToken> tokens, IReadOnlyList<BlockPosition> skippedTokens,
            IReadOnlyList<HeddleDiagnostic> diagnostics, IReadOnlyList<DefinitionInfo> definitions,
            IReadOnlyList<ImportLink> imports, ScopeMapView scopes, bool cSharpTierUsed)
        {
            Path = path;
            Version = version;
            Text = text;
            Lines = lines;
            Tokens = tokens;
            SkippedTokens = skippedTokens;
            Diagnostics = diagnostics;
            Definitions = definitions;
            Imports = imports;
            Scopes = scopes;
            CSharpTierUsed = cSharpTierUsed;
        }

        public string Path { get; }
        public int Version { get; }
        public string Text { get; }
        public LineMap Lines { get; }
        public IReadOnlyList<HeddleToken> Tokens { get; }
        public IReadOnlyList<BlockPosition> SkippedTokens { get; }
        public IReadOnlyList<HeddleDiagnostic> Diagnostics { get; }
        public IReadOnlyList<DefinitionInfo> Definitions { get; }
        public IReadOnlyList<ImportLink> Imports { get; }
        public ScopeMapView Scopes { get; }

        /// <summary>True when the Roslyn (C# tier) pass ran for this analysis (D9).</summary>
        public bool CSharpTierUsed { get; }
    }
}
