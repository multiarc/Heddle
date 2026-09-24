using System;
using System.Collections.Generic;

namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>Merges single-template artifacts into one assembly artifact. The build host compiles
    /// every template through its own <c>CompileContext</c> (per-item options and model type), so each
    /// record converts to an artifact whose row indices are local; the merged artifact re-bases every
    /// index onto the concatenated tables in response-file order. Deterministic: equal inputs in equal
    /// order encode to identical bytes through the writer's fixed walk.</summary>
    internal static class CompiledArtifactMerger
    {
        internal static CompiledArtifact Merge(IReadOnlyList<CompiledArtifact> parts)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));
            if (parts.Count == 0)
                throw new ArgumentException("At least one artifact is required.", nameof(parts));
            if (parts.Count == 1)
                return parts[0];
            // Rows in ordinal key order, whatever order the parts arrived in, so item or
            // response-file order cannot change the artifact's bytes or digest.
            var ordered = new List<CompiledArtifact>(parts);
            ordered.Sort((a, b) => string.CompareOrdinal(FirstKey(a), FirstKey(b)));
            var merged = new CompiledArtifact { Header = ordered[0].Header };
            foreach (var part in ordered)
            {
                if (part == null)
                    throw new ArgumentException("Artifact parts must not be null.", nameof(parts));
                int extensions = merged.Extensions.Count;
                int functions = merged.Functions.Count;
                int members = merged.Members.Count;
                int expressions = merged.Expressions.Count;
                int csharp = merged.CSharpSites.Count;
                int documents = merged.Documents.Count;
                int definitions = merged.Definitions.Count;
                int templates = merged.Templates.Count;
                int sites = merged.Sites.Count;
                foreach (var row in part.Extensions)
                    merged.Extensions.Add(row);
                foreach (var row in part.Functions)
                    merged.Functions.Add(row);
                foreach (var row in part.Members)
                    merged.Members.Add(row);
                foreach (var row in part.Expressions)
                    merged.Expressions.Add(row);
                foreach (var row in part.CSharpSites)
                    merged.CSharpSites.Add(row);
                foreach (var document in part.Documents)
                {
                    ShiftDocument(document, extensions, members, expressions, sites, definitions,
                        documents);
                    merged.Documents.Add(document);
                }

                foreach (var definition in part.Definitions)
                {
                    ShiftDefinition(definition, documents);
                    merged.Definitions.Add(definition);
                }

                foreach (var row in part.Templates)
                {
                    ShiftTemplate(row, extensions, functions, documents, definitions);
                    merged.Templates.Add(row);
                }

                foreach (var row in part.Sites)
                {
                    row.TemplateIndex += templates;
                    row.PayloadRef += PayloadBase(row.Kind, members, expressions, csharp, documents);
                    merged.Sites.Add(row);
                }
            }

            return merged;
        }

        private static string FirstKey(CompiledArtifact part)
        {
            if (part == null || part.Templates == null || part.Templates.Count == 0 || part.Templates[0] == null)
                return string.Empty;
            return part.Templates[0].Key ?? string.Empty;
        }

        private static int PayloadBase(CompiledSiteKind kind, int members, int expressions, int csharp,
            int documents)
        {
            switch (kind)
            {
                case CompiledSiteKind.MemberAccessor:
                    return members;
                case CompiledSiteKind.NativeExpression:
                case CompiledSiteKind.LateBoundCall:
                    return expressions;
                case CompiledSiteKind.EmbeddedCSharp:
                    return csharp;
                case CompiledSiteKind.Refusal:
                    // A refusal row's payload indexes its own template row's RefusalSites list,
                    // which is per row and never rebased.
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), "Unknown site kind '" + kind + "'.");
            }
        }

        private static void ShiftTemplate(CompiledTemplateRow row, int extensions, int functions,
            int documents, int definitions)
        {
            for (int i = 0; i < row.ExtensionRefs.Count; i++)
                row.ExtensionRefs[i] += extensions;
            for (int i = 0; i < row.FunctionRefs.Count; i++)
                row.FunctionRefs[i] += functions;
            row.RootDocumentRef += documents;
            for (int i = 0; i < row.DefinitionRefs.Count; i++)
                row.DefinitionRefs[i] += definitions;
        }

        private static void ShiftDefinition(CompiledDefinition definition, int documents)
        {
            if (definition.Fills != null)
                foreach (var fill in definition.Fills)
                    fill.DocumentRef += documents;
        }

        private static void ShiftDocument(CompiledDocument document, int extensions, int members,
            int expressions, int sites, int definitions, int documents)
        {
            if (document.Elements != null)
                foreach (var element in document.Elements)
                {
                    if (element == null || !element.IsChain || element.Chain == null ||
                        element.Chain.Items == null)
                        continue;
                    foreach (var item in element.Chain.Items)
                        ShiftItem(item, extensions, members, expressions, sites, definitions, documents);
                }

            if (document.RemovedItems != null)
                foreach (var item in document.RemovedItems)
                    ShiftItem(item, extensions, members, expressions, sites, definitions, documents);
            if (document.ParseFacts != null && document.ParseFacts.VisibleDefinitionRefs != null)
                for (int i = 0; i < document.ParseFacts.VisibleDefinitionRefs.Count; i++)
                    document.ParseFacts.VisibleDefinitionRefs[i] += definitions;
        }

        private static void ShiftItem(CompiledItem item, int extensions, int members, int expressions,
            int sites, int definitions, int documents)
        {
            if (item == null)
                return;
            item.ExtensionRef += extensions;
            ShiftParameter(item.Parameter, extensions, members, expressions, sites, definitions,
                documents);
            if (item.Body != null && item.Body.CompiledDocumentRef.HasValue)
                item.Body.CompiledDocumentRef = item.Body.CompiledDocumentRef.Value + documents;
            if (item.AltBodies != null)
                foreach (var alt in item.AltBodies)
                    if (alt != null && alt.Body != null && alt.Body.CompiledDocumentRef.HasValue)
                        alt.Body.CompiledDocumentRef = alt.Body.CompiledDocumentRef.Value + documents;
            if (item.Props != null && item.Props.DynamicSlots != null)
                foreach (var slot in item.Props.DynamicSlots)
                    if (slot != null)
                        slot.ExpressionRef += expressions;
        }

        private static void ShiftParameter(CompiledParameter parameter, int extensions, int members,
            int expressions, int sites, int definitions, int documents)
        {
            if (parameter == null)
                return;
            parameter.MemberRef += members;
            parameter.ExpressionRef += expressions;
            parameter.SiteRef += sites;
            parameter.DefinitionRef += definitions;
            parameter.CallerContentRef += documents;
            if (parameter.NestedChain != null && parameter.NestedChain.Items != null)
                foreach (var nested in parameter.NestedChain.Items)
                    ShiftItem(nested, extensions, members, expressions, sites, definitions, documents);
        }
    }
}
