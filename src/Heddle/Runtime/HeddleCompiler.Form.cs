using System;
using Heddle.Language;
using Heddle.Precompiled.CompiledForm;

namespace Heddle.Runtime
{
    /// <summary>The compiled-form materialization entry: replays a decoded artifact row through the text
    /// path's own machinery. The recorded shaped text is parsed by the real parser, extensions are created
    /// by <c>TemplateFactory</c>, render types derive and hooks run exactly as on a text compile; only the
    /// body source differs — a hook's body compile is served from the form by <see cref="FormCursor"/>
    /// (post-state 1, 2 or 3) with the recorded consumed types checked, and a named child resolves from the
    /// same artifact before the engine falls back to its dynamic compile under the request options.
    /// <para>The caller owns the <see cref="CompileScope"/> (model type and options preset, as the registry
    /// will) and checks <c>scope.CompileErrors</c> afterwards: a typing fault or hook error surfaces there,
    /// never as an exception. The cursor touches no <c>Microsoft.CodeAnalysis</c> type; embedded-C# sites
    /// stay data until phase 3.</para></summary>
    internal partial class HeddleCompiler
    {
        internal static RuntimeDocument Materialize(CompiledArtifact artifact, CompiledTemplateRow row,
            CompileScope scope)
        {
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            if (row == null)
                throw new ArgumentNullException(nameof(row));
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));
            if (artifact.Documents == null || row.RootDocumentRef < 0 ||
                row.RootDocumentRef >= artifact.Documents.Count ||
                artifact.Documents[row.RootDocumentRef] == null)
                throw new ArgumentOutOfRangeException(nameof(row),
                    "Row '" + row.Key + "' names no document in this artifact.");

            var cursor = FormCursor.OverArtifact(artifact, row.Key);
            cursor.EnterRoot(row.RootDocumentRef);
            var previousSlot = scope.FormCursor;
            scope.FormCursor = cursor;
            using (cursor.ArmAmbient())
            {
                try
                {
                    // The raw text: the exact text the build parsed, so the re-parse reproduces
                    // the build's items at the recorded positions. Shaped text is not reparseable
                    // (collapsed @@ escapes, removed definitions, trimmed lines).
                    string raw = FormCursor.SourceText(artifact.Documents[row.RootDocumentRef]);
                    var parseContext = DocumentParser.Parse(raw, scope.CompileContext,
                        out var optimizedDocument);
                    var document = HeddleCompiler.Compile(optimizedDocument, scope, parseContext, null);
                    scope.Compile();
                    return document;
                }
                finally
                {
                    scope.FormCursor = previousSlot;
                }
            }
        }
    }
}
