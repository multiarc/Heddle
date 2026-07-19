using System.Linq;

namespace Heddle.LanguageServices.Completion
{
    /// <summary>
    /// Go-to-definition (phase 6 D16/D26): a definition reference targets the surviving registry entry's header
    /// span in its owning file; an <c>@&lt;&lt;</c>/<c>@partial</c> site targets the resolved file at 0..0; a
    /// prop named-argument targets the <c>PropDeclaration</c> span. Unresolvable → null (never a guess).
    /// </summary>
    internal static class DefinitionProvider
    {
        internal static DefinitionTarget GetDefinition(DocumentAnalysis analysis, int offset)
        {
            // Import / partial site → the target file at position 0.
            foreach (var link in analysis.Imports)
            {
                if (offset >= link.Offset && offset < link.Offset + link.Length && link.ResolvedPath != null)
                    return new DefinitionTarget(link.ResolvedPath, 0, 0);
            }

            var (word, _, _) = HoverProvider.WordAt(analysis.Text, offset);
            if (string.IsNullOrEmpty(word))
                return null;

            // A prop named-argument name inside a definition call → the prop declaration span.
            var context = ContextDetector.Detect(analysis, offset, out _);
            if (context.Kind == CompletionContextKind.NamedArgument)
            {
                var callee = analysis.Definitions.FirstOrDefault(d => d.Name == context.CallName);
                var prop = callee?.Props.FirstOrDefault(p => p.Name == word);
                if (prop != null)
                    return new DefinitionTarget(callee.SourcePath, prop.DeclarationOffset, prop.DeclarationLength);
            }

            // Definition call/name → the surviving registry entry's header (D26 — analysis.Definitions holds it).
            var definition = analysis.Definitions.FirstOrDefault(d => d.Name == word);
            if (definition != null)
                return new DefinitionTarget(definition.SourcePath, definition.Offset, definition.Length);

            return null;
        }
    }
}
