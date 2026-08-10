using Heddle.Generator.Binding;
using Heddle.Language;
using Heddle.Language.Expressions;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Typing
{
    /// <summary>
    /// The pure typing rules of the emitter's Pass 2 — functions over a <see cref="BodyContext"/> and node facts,
    /// with no Roslyn emission dependency and no emitter state. Each rule answers "what typing environment does
    /// this body/value get", never "what code is written for it"; the emitter supplies the symbols and consumes
    /// the derived context.
    /// </summary>
    internal static class BodyTypingRules
    {
        /// <summary>
        /// The typing environment a nested body is built in, given the role its host's hook actually put it in.
        /// <list type="bullet">
        /// <item><description><see cref="BodyModelSource.Observed"/> — the body's model is the type named,
        /// which is what an observed engine compile reports for the body's span. The enclosing fill scope, region
        /// props, slot mode and — the engine restores the prop layout around <b>definition</b> bodies only — the
        /// enclosing <b>prop layout</b> all propagate.
        /// <para>Emitting the reads on the dynamic tier regardless cost this both ways. A member the body's model
        /// does <em>not</em> carry is an <c>HED0001</c> the engine raises when it compiles the template; bound
        /// dynamically it precompiled and threw <c>RuntimeBinderException</c> at render, and over a model of
        /// <c>object</c> — a static type, not the absence of one — it precompiled and <b>rendered</b> a page the
        /// engine refuses outright. And every read the dynamic tier cannot express — a native expression over the
        /// model's own member, a function call taking one — degraded a template the engine renders.</para>
        /// </description></item>
        /// <item><description><see cref="BodyModelSource.Data"/> — the body's model is the call's own data value,
        /// which is what a <c>[ChildTemplateHost]</c> extension's author declares by carrying that
        /// attribute.</description></item>
        /// </list>
        /// <para>A role the emitter has no emission for returns <c>false</c>: the caller refuses the body and the
        /// template degrades, which is the safe direction.</para>
        /// </summary>
        internal static bool TryNestedBodyContext(BodyModelSource source, BodyContext bctx,
            ITypeSymbol bodyModel, out BodyContext nested)
        {
            nested = bctx;

            // The host's own data value IS the body's model, so the type handed in is the whole answer — the same
            // shape the observed role takes, from a different question.
            if (source == BodyModelSource.Data)
                return TryDataValueBodyContext(bodyModel, bctx, out nested);

            if (source == BodyModelSource.Observed)
            {
                // The engine compiled this body against a dynamic scope and the enclosing one already IS that
                // scope, so the enclosing context is the answer rather than a fresh dynamic one built beside it.
                // Rebuilding it drops what a context carries besides its model — the late-body sink, the slot mode,
                // the engine's own typing of the dynamic model — and every one of those is a member the body could
                // otherwise read.
                if (bodyModel == null && bctx.IsDynamic)
                    return true;

                var bodyCtx = bodyModel == null || bodyModel.TypeKind == TypeKind.Dynamic
                    ? new BodyContext(null, null, true, props: bctx.Props,
                        fills: bctx.Fills, regionHostProps: bctx.RegionHostProps, dynamicBodyModel: bodyModel,
                        root: bctx.Root, chained: bctx.Chained)
                    : new BodyContext("(" + SymbolTypeResolver.FullyQualified(bodyModel) + ")", bodyModel,
                        false, props: bctx.Props, fills: bctx.Fills, regionHostProps: bctx.RegionHostProps,
                        dynamicBodyModel: bodyModel, root: bctx.Root, chained: bctx.Chained);
                nested = bctx.InSlot ? bodyCtx.AsSlot(bctx.SlotType) : bodyCtx;
                return true;
            }

            return false;
        }

        /// <summary>The typing environment of a body the host compiles against its own data value — the engine
        /// hands <c>InitSubTemplate</c> the call's <c>dataType</c> — dynamic where that scope is dynamic. False
        /// when the caller is typed but the value's type cannot be said, where a guess could change what the body
        /// evaluates to.</summary>
        internal static bool TryDataValueBodyContext(ITypeSymbol childModel, BodyContext bctx, out BodyContext nameCtx)
        {
            if (childModel == null && !bctx.IsDynamic)
            {
                nameCtx = bctx;
                return false;
            }

            nameCtx = childModel == null || childModel.TypeKind == TypeKind.Dynamic
                ? new BodyContext(null, null, true, props: bctx.Props, fills: bctx.Fills,
                    regionHostProps: bctx.RegionHostProps, dynamicBodyModel: childModel,
                    root: bctx.Root, chained: bctx.Chained)
                : new BodyContext("(" + SymbolTypeResolver.FullyQualified(childModel) + ")", childModel, false,
                    props: bctx.Props, fills: bctx.Fills, regionHostProps: bctx.RegionHostProps,
                    dynamicBodyModel: childModel, root: bctx.Root, chained: bctx.Chained);
            if (bctx.InSlot)
                nameCtx = nameCtx.AsSlot(bctx.SlotType);
            return true;
        }

        /// <summary>True when this path resolves prop-first: no target, no <c>::</c> root, and a first segment the
        /// active layout carries. Once that holds the model is out of the picture — a first segment naming a prop
        /// never falls back to the member it shadows.</summary>
        internal static bool IsPropName(PathNode path, PropLayoutInfo props) =>
            path.Target == null && !path.RootRef && props != null && path.Segments.Count != 0 &&
            props.ByName.ContainsKey(path.Segments[0]);

        /// <summary>The prop a bare member path's first segment shadows the model with, or null where the layout
        /// carries no such name. The same segment-0 rule as <see cref="IsPropName"/>, for the call shapes that
        /// arrive as segments rather than a <see cref="PathNode"/>.</summary>
        internal static PropSlotInfo PropShadowSlot(PropLayoutInfo props, string firstSegment) =>
            props != null && firstSegment != null && props.ByName.TryGetValue(firstSegment, out var slot)
                ? slot
                : null;
    }
}
