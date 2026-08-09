using System.Collections.Generic;
using Heddle.Language;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Typing
{
    /// <summary>
    /// Definition-body typing shared by identity, the way the engine shares it: one compiled body per
    /// <see cref="ParseContext"/> the definition is reached through, typed by whichever call site arrived first.
    /// The memo is the only state the typing pass keeps, and it is per-template — the emitter holds one instance
    /// per emit and the rules in <see cref="BodyTypingRules"/> never touch it.
    /// </summary>
    internal sealed class BodyTypingMemo
    {
        private readonly Dictionary<string, BodyContext> _sharedBodyTyping =
            new Dictionary<string, BodyContext>(System.StringComparer.Ordinal);

        // ParseContext declares no equality of its own, so the dictionary keys by reference — which is the
        // question being asked: whether the two call sites reached the same parsed body or an isolated copy of it.
        private readonly Dictionary<ParseContext, int> _parseContextIds = new Dictionary<ParseContext, int>();

        /// <summary>The body-identity key for a definition's parse context — the term both the sharing rule and
        /// the emitter's emitted-code cache key by, because they are two halves of one question: which call sites
        /// the engine gives one compiled body to.</summary>
        internal string KeyOf(ParseContext context) =>
            ParseContextId(context).ToString(System.Globalization.CultureInfo.InvariantCulture);

        private int ParseContextId(ParseContext context)
        {
            if (context == null)
                return 0;
            if (!_parseContextIds.TryGetValue(context, out var id))
                _parseContextIds[context] = id = _parseContextIds.Count + 1;
            return id;
        }

        /// <summary>
        /// The engine compiles a definition body once per <see cref="ParseContext"/> the definition is reached
        /// through — not once per call site. Every item it compiles is memoized for the whole compile, so a second
        /// call site into one definition re-uses the code the first one produced, and its own value is simply cast
        /// to the model the first one typed that code against. Two call sites reach two contexts, and so two
        /// compiles, only where the parser isolated the definition tree between them: a document-scope output chain
        /// and a subtemplate outside a definition body both isolate, and inside a definition body nothing does — one
        /// body there serves every call, however many models the calls hand it.
        /// <para>So the emitter shares by the same measure, and the call site that arrives second is re-typed to the
        /// body that already exists. Where that body is typed, its <c>(T)scope.ModelData</c> is the engine's cast and
        /// reproduces it exactly, failure included. Where it is on the dynamic tier there is no cast to reproduce —
        /// its reads bind to whatever they are handed — so a later call site of another model degrades instead of
        /// reading members off a value the engine would have refused to cast.</para>
        /// <para>Two typings are the same when they agree on the tier and on the model symbol. A third term for
        /// <c>DynamicBodyModel</c> would decide nothing: every context that reaches here got its model from
        /// <c>TemplateEmitter.DefinitionBodyContext</c> or <c>TemplateEmitter.TryTypeCallSiteBody</c>, and both
        /// leave the two in step — a typed body carries its own model in both, and an untyped one is untyped
        /// precisely because the model is the compilation's <c>dynamic</c>.</para>
        /// </summary>
        internal bool TryShareBodyTyping(string key, ref BodyContext bodyCtx, out string reason)
        {
            reason = null;
            if (!_sharedBodyTyping.TryGetValue(key, out var first))
            {
                _sharedBodyTyping[key] = bodyCtx;
                return true;
            }

            if (first.IsDynamic == bodyCtx.IsDynamic &&
                SymbolEqualityComparer.Default.Equals(first.ModelSymbol, bodyCtx.ModelSymbol))
                return true;

            if (first.IsDynamic)
            {
                reason = "definition body already compiled untyped for a call site of another model";
                return false;
            }

            bodyCtx = first;
            return true;
        }
    }
}
