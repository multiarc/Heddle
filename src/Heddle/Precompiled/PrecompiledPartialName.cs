using Heddle.Data;
using Heddle.Exceptions;

namespace Heddle.Precompiled
{
    /// <summary>
    /// <b>Retired in place</b>: the generator no longer emits a field of this type, because a precompiled
    /// child-template host evaluates its own name in its own <c>InitStart</c>. It stays, and keeps working,
    /// because it is public and generated code from earlier generator versions holds it — removing it would
    /// fault every such assembly. Removal is a next-window candidate.
    /// <para>The once-evaluated name of a computed-name <c>@partial</c> in a precompiled template. The dynamic engine
    /// evaluates such a name body a single time, at its compile time, against <see cref="Scope.Null"/>
    /// (<c>PartialExtension.InitStart</c>); generated code reproduces that with a static field initialized through
    /// <see cref="PrecompiledRuntime.EvaluatePartialName"/> and reads it here on every render.</para>
    /// <para>An evaluation that threw is the engine's compile fault for the call, and the engine surfaces a failed
    /// compile as a <see cref="TemplateCompileException"/> from every <c>Generate</c> — so <see cref="Get"/>
    /// re-raises the recorded fault on every read, at the precompiled tier's first observable moment.</para>
    /// </summary>
    public sealed class PrecompiledPartialName
    {
        private readonly string _name;
        private readonly HeddleCompileError _error;

        internal PrecompiledPartialName(string name) => _name = name;

        internal PrecompiledPartialName(HeddleCompileError error) => _error = error;

        /// <summary>The evaluated, trimmed name — empty when the name body produced nothing, in which case the
        /// call renders no partial at all. Throws <see cref="TemplateCompileException"/> when the evaluation faulted,
        /// exactly as the engine's <c>Generate</c> throws for the compile its fault failed.</summary>
        public string Get()
        {
            if (_error != null)
                throw new TemplateCompileException(new[] { _error });
            return _name;
        }
    }
}
