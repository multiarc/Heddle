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
        /// <para>The nested body's model context is <b>derived from</b> <see cref="BodyModelRules"/>' row for the
        /// host name, not chosen per emission branch. This is the emitter's real dependency on the table: the row
        /// decides which context the body is built in, so the table is load-bearing.</para>
        /// <list type="bullet">
        /// <item><description><see cref="BodyModelSource.Parent"/> (the branch trio, <c>@for</c>) — the body keeps
        /// the enclosing typed context, because it executes under <c>scope.Parent()</c>.</description></item>
        /// <item><description><see cref="BodyModelSource.ElementOfData"/> (<c>@list</c>) — the body is typed by the
        /// element type, because that is the type the engine compiles it against: <c>ListExtension.InitStart</c>
        /// returns the collection's <c>IEnumerable&lt;T&gt;</c> argument and the body is compiled once, in a scope of
        /// that type. Only where the host reaches no generic form does it hand back <c>ExType.Dynamic</c>, and only
        /// then is the body genuinely untyped. The enclosing fill scope, region props, slot mode and — the engine
        /// restores the prop layout around <b>definition</b> bodies only — the enclosing <b>prop layout</b> all
        /// propagate.
        /// <para>Emitting the reads on the dynamic tier regardless cost this both ways. A member the element type
        /// does <em>not</em> carry is an <c>HED0001</c> the engine raises when it compiles the template; bound
        /// dynamically it precompiled and threw <c>RuntimeBinderException</c> at render, and over an element type of
        /// <c>object</c> — a static type, not the absence of one — it precompiled and <b>rendered</b> a page the
        /// engine refuses outright. And every read the dynamic tier cannot express — a native expression over the
        /// element's own member, a function call taking one — degraded a template the engine renders.</para></description></item>
        /// </list>
        /// <para>A name with no pinned row, or a row naming a source the emitter has no emission for, returns
        /// <c>false</c>: the caller refuses the body and the template degrades, which is the safe direction.</para>
        /// </summary>
        internal static bool TryNestedBodyContext(string name, BodyContext bctx, ITypeSymbol elementModel,
            out BodyContext nested)
        {
            nested = bctx;
            return BodyModelRules.TryGet(name, out var source, out _) &&
                   TryNestedBodyContext(source, bctx, elementModel, out nested);
        }

        /// <summary>The same rule, given the role directly rather than a name to look it up by, so an extension
        /// can be typed by what its own hook did rather than by whether the emitter happened to know its
        /// name.</summary>
        internal static bool TryNestedBodyContext(BodyModelSource source, BodyContext bctx,
            ITypeSymbol elementModel, out BodyContext nested)
        {
            nested = bctx;
            if (source == BodyModelSource.Parent)
                return true;

            // The host's own data value IS the body's model, so the type handed in is the whole answer — the same
            // shape the element role takes, from a different question.
            if (source == BodyModelSource.Data)
                return TryDataValueBodyContext(elementModel, bctx, out nested);

            if (source == BodyModelSource.ElementOfData)
            {
                var elementCtx = elementModel == null || elementModel.TypeKind == TypeKind.Dynamic
                    ? new BodyContext(null, null, true, props: bctx.Props,
                        fills: bctx.Fills, regionHostProps: bctx.RegionHostProps, dynamicBodyModel: elementModel,
                        root: bctx.Root, chained: bctx.Chained)
                    : new BodyContext("(" + SymbolTypeResolver.FullyQualified(elementModel) + ")", elementModel,
                        false, props: bctx.Props, fills: bctx.Fills, regionHostProps: bctx.RegionHostProps,
                        dynamicBodyModel: elementModel, root: bctx.Root, chained: bctx.Chained);
                nested = bctx.InSlot ? elementCtx.AsSlot(bctx.SlotType) : elementCtx;
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

        /// <summary>The slot-value typing decision, given the value's static type (null = "cannot say", exempt):
        /// a value with no static type is the engine's first HED5014 arm and is refused outright — C#'s own table
        /// would say yes to every <c>dynamic</c> — and a typed one must pass the conversion the caller supplies
        /// (<c>PropConversion.CanConvert</c> semantics with boxing disallowed, exactly as <c>OutExtension.InitStart</c>
        /// asks it).</summary>
        internal static bool TrySlotValue(ITypeSymbol valueType, ITypeSymbol slotType,
            System.Func<ITypeSymbol, ITypeSymbol, bool> convertibleWithoutBoxing, out string reason)
        {
            reason = null;
            if (valueType == null)
                return true;

            if (valueType.TypeKind == TypeKind.Dynamic)
            {
                reason = "slot value is dynamic under a dynamic definition model";
                return false;
            }

            if (convertibleWithoutBoxing(valueType, slotType))
                return true;

            reason = "slot value '" + SymbolTypeResolver.FullyQualified(valueType) + "' is not assignable to slot type '" +
                     SymbolTypeResolver.FullyQualified(slotType) + "'";
            return false;
        }
    }
}
