using System;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Strings.Core;

namespace Heddle.Runtime {
    /// <summary>One extension instance bound to one call site. The compiler drives the first three
    /// members once per site, in order; the renderer calls one of the two render members per render.
    /// Derive from <c>AbstractExtension</c> rather than implementing this directly.</summary>
    public interface IExtension: IDisposable {
        /// <summary>Compile time, first: the encoding posture the site renders under, derived from the
        /// output profile and the extension's encoding attributes.</summary>
        void SetUpRenderType(RenderType renderType);

        /// <summary>Compile time: binds the site to its argument, chained and parent types and returns
        /// the type the site produces, which types the next link of a chain. Null declares a site that
        /// emits nothing.</summary>
        ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent);

        /// <summary>Compile time, last: a second pass once the site's body scope exists, for extensions
        /// that need to see their subtemplate.</summary>
        void CompleteInit(CompileScope newScope, ParseContext parseContext);

        /// <summary>Render time: computes the site's value for <paramref name="scope"/> as an object, the
        /// shape a chain link or an argument consumes. Must agree with <see cref="RenderData"/>.</summary>
        object ProcessData(in Scope scope);

        /// <summary>Render time: writes the site's output to the scope's renderer, the direct path the
        /// engine prefers when nothing consumes the value.</summary>
        void RenderData(in Scope scope);

        /// <summary>The call site's position in the template source.</summary>
        BlockPosition Position { get; set; }
    }
}