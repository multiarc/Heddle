using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Runtime {
    internal class ExpressionOptions {

        public ExType RootModelType { get; set; }

        public ExType ModelType { get; set; }

        public ExType ChainedType { get; set; }

        /// <summary>The three scope types as the generated C# declares them: spelled in full, so a nested
        /// type, or a generic argument from a namespace nothing imports, compiles.</summary>
        public string ModelCSharpType => Reference(ModelType);

        public string ChainedCSharpType => Reference(ChainedType);

        public string RootModelCSharpType => Reference(RootModelType);

        private static string Reference(ExType type)
        {
            if (type == null || type.IsDynamic)
                return type?.ToString();
            return Heddle.Helpers.TypeNameHelper.GetCSharpReference(type.Type);
        }

        public string ExtensionName { get; set; }

        public string Expression { get; set; }

        public BlockPosition Position { get; set; }

        public IEnumerable<string> Namespaces { get; set; }

        /// <summary><see cref="Namespaces"/> as an emitted <c>using</c> directive has to spell them: a part
        /// named like a C# keyword escaped.</summary>
        public IEnumerable<string> CSharpNamespaces =>
            Namespaces?.Select(Helpers.TypeNameHelper.EscapeDottedName) ?? Enumerable.Empty<string>();

        /// <summary>When set, an unbound function name defers (late-bound site) instead of failing.
        /// Off on every dynamic-tier compile; the build tier arms it while recording the form.</summary>
        internal bool DeferUnboundFunctions { get; set; }
    }

    /// <summary>The deferred-call marker: the result type of a site the build could not bind. It shapes
    /// nothing — <c>HeddleCompiler.CheckTypes</c> and the prop-slot checks accept it without a verdict —
    /// and it never reaches the artifact (the writer maps it to an untyped slot). A hook that would type
    /// from it in a bodied or chained consumer triggers the class-(c) refusal instead.</summary>
    internal static class DeferredResult
    {
        internal sealed class Marker
        {
            private Marker()
            {
            }
        }

        internal static readonly ExType Deferred = new ExType(typeof(Marker));

        internal static bool IsDeferred(ExType type) => type != null && type.Type == typeof(Marker);
    }
}