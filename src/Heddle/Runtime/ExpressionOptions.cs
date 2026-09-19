using System.Collections.Generic;
using Heddle.Data;
using Heddle.Strings.Core;

namespace Heddle.Runtime {
    internal class ExpressionOptions {

        public ExType RootModelType { get; set; }

        public ExType ModelType { get; set; }

        public ExType ChainedType { get; set; }

        public string ExtensionName { get; set; }

        public string Expression { get; set; }

        public BlockPosition Position { get; set; }

        public IEnumerable<string> Namespaces { get; set; }

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