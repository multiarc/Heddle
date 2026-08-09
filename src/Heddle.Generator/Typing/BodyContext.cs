using System.Collections.Generic;
using Heddle.Generator.Binding;
using Heddle.Language;
using Microsoft.CodeAnalysis;

namespace Heddle.Generator.Typing
{
    /// <summary>
    /// The typing environment a body is compiled under — the emitter's explicit <c>TypeEnv</c>: the model
    /// (<see cref="ModelSymbol"/>/<see cref="ModelCast"/>/<see cref="IsDynamic"/>), the constant root, the chained
    /// value, the active prop layout, the slot type and the ambient region fills. Every typing rule in
    /// <see cref="BodyTypingRules"/> is a pure function over one of these plus node facts; the emitter only
    /// carries it.
    /// </summary>
    internal readonly struct BodyContext
    {
        public BodyContext(string modelCast, ITypeSymbol modelSymbol, bool isDynamic, PropLayoutInfo props = null,
            ITypeSymbol slotType = null, Dictionary<string, DefinitionItem> fills = null,
            PropLayoutInfo regionHostProps = null, ITypeSymbol dynamicBodyModel = null,
            ITypeSymbol root = null, ITypeSymbol chained = null, LateBodySink late = null)
        {
            Late = late;
            ModelCast = modelCast;
            ModelSymbol = modelSymbol;
            IsDynamic = isDynamic;
            Props = props;
            SlotType = slotType;
            Fills = fills;
            RegionHostProps = regionHostProps;
            DynamicBodyModel = dynamicBodyModel;
            Root = root;
            Chained = chained;
        }

        public string ModelCast { get; }          // "(global::T)" or null for the dynamic tier

        /// <summary>The model's type for member-path typing. Every construction site upholds
        /// <c>IsDynamic ⇒ ModelSymbol is null</c> — the dynamic tier has no static model to type against — so a
        /// reader wanting "the typed model here, if any" reads this alone. A body emitted on the dynamic tier
        /// over a model the <b>engine</b> has typed carries that type in <see cref="DynamicBodyModel"/>, which
        /// is a different question with a different answer.</summary>
        public ITypeSymbol ModelSymbol { get; }

        public bool IsDynamic { get; }

        /// <summary>The active prop layout: a body prop read wins over the model on the first path
        /// segment. Null outside a definition body with props. Prop-first resolution is syntactic, so both
        /// backends resolve identically.</summary>
        public PropLayoutInfo Props { get; }

        /// <summary>The declared slot parameter type inside a slot-declaring definition body, null outside one.
        /// Carried rather than a bare flag because every <c>@out(value)</c> here has to be checked against it,
        /// the way the engine checks it when it compiles the same body.</summary>
        public ITypeSymbol SlotType { get; }

        /// <summary>True inside a slot-declaring definition body: <c>@out(value)</c> projects the caller content
        /// (slot mode); outside a slot definition, an <c>@out</c> value is a runtime error the emitter refuses.</summary>
        public bool InSlot => SlotType != null;

        /// <summary>The ambient region fill scope — <c>regionName → materialized-fill DefinitionItem</c> —
        /// the generator's parallel to the dynamic tier's <c>RegionFillScope</c> on <c>CompileContext</c>.
        /// Threaded through every nested body build (branch/list/for bodies and the definition-body contexts)
        /// so a fill resolves at any depth. Null outside a filled component body.</summary>
        public Dictionary<string, DefinitionItem> Fills { get; }

        /// <summary>The enclosing component's prop layout, carried through nested bodies so a region body
        /// borrows the component's props (a region declares none of its own).</summary>
        public PropLayoutInfo RegionHostProps { get; }

        /// <summary>The model behind a body emitted on the dynamic tier, where <see cref="ModelSymbol"/> is
        /// null but the engine still has a static type in hand.
        /// <para>Two bodies set it, and both set it alongside the model they are typed by, so the two never
        /// disagree. A <c>:: dynamic</c> definition body carries the model of the one call site that built it:
        /// the engine compiles such a definition once per call site off the model that call site hands it, so
        /// the declaration does not mean "untyped", it means "whatever this caller passes". A nested
        /// <c>@list</c> body carries the <b>element</b> type for exactly the same reason. Either is on the
        /// dynamic tier only when the value <em>is</em> the compilation's <c>dynamic</c> — then the body
        /// genuinely has no static model, and that is what the <c>@out</c> check reads. Null on the typed tier
        /// and in a body with neither source.</para>
        /// <para>Two call sites that hand the same <c>:: dynamic</c> definition different models do not get
        /// different bodies — the engine gives them one, and <see cref="BodyTypingMemo.TryShareBodyTyping"/> is
        /// where that is decided.</para></summary>
        public ITypeSymbol DynamicBodyModel { get; }

        /// <summary>The root model's type — what an embedded expression's <c>root</c> parameter is spelled as.
        /// Constant at every depth (the engine's <c>RootScopeType</c> is set once and every <c>Scope</c>
        /// transform passes <c>RootData</c> through verbatim), so every construction site seeds it with the
        /// entry model and every derivation carries it. Null means the root has no pinned static type and the
        /// parameter is spelled <c>dynamic</c>.</summary>
        public ITypeSymbol Root { get; }

        /// <summary>The chained value's compile-time type for an embedded expression — null today for every
        /// reachable one, spelled <c>dynamic</c>: the engine types <c>chained</c> as
        /// <c>returnTypeChainedPrevious ?? ExType.Dynamic</c> and only a mid-chain expression, which the
        /// emitter refuses elsewhere, ever sees a previous item. Carried so the typing pass that threads a
        /// chain's real type has a seam to fill.</summary>
        public ITypeSymbol Chained { get; }

        /// <summary>Non-null inside a body the build emitted <b>type-agnostically</b>: the extension hosting it
        /// decides the body's model type in a hook the build has not read, so nothing here may be spelled against
        /// a model type. Every model-dependent read and every nested call site registers on this sink instead, and
        /// <c>PrecompiledRuntime.Init</c> resolves them once the hook has answered.
        /// <para>Distinct from <see cref="IsDynamic"/>, which says the model genuinely has no static type on either
        /// tier. Here the engine has a type and the build does not know it yet.</para></summary>
        public LateBodySink Late { get; }

        public BodyContext WithProps(PropLayoutInfo props) =>
            new BodyContext(ModelCast, ModelSymbol, IsDynamic, props, SlotType, Fills, RegionHostProps,
                DynamicBodyModel, Root, Chained, Late);

        public BodyContext AsSlot(ITypeSymbol slotType) =>
            new BodyContext(ModelCast, ModelSymbol, IsDynamic, Props, slotType, Fills, RegionHostProps,
                DynamicBodyModel, Root, Chained, Late);

        /// <summary>The same body, now typed by the model the call site actually hands it.</summary>
        public BodyContext TypedAs(ITypeSymbol model) =>
            new BodyContext("(" + SymbolTypeResolver.FullyQualified(model) + ")", model, false, Props, SlotType,
                Fills, RegionHostProps, model, Root, Chained);

        public BodyContext WithDynamicBodyModel(ITypeSymbol model) =>
            new BodyContext(ModelCast, ModelSymbol, IsDynamic, Props, SlotType, Fills, RegionHostProps, model,
                Root, Chained, Late);

        public BodyContext WithFills(Dictionary<string, DefinitionItem> fills, PropLayoutInfo regionHostProps) =>
            new BodyContext(ModelCast, ModelSymbol, IsDynamic, Props, SlotType, fills, regionHostProps,
                DynamicBodyModel, Root, Chained, Late);

        /// <summary>The body an unread hook types: no model cast, no static model, and a sink for everything
        /// model-dependent inside it. Props, slot and fills stay as they are — a prop read is indexed rather than
        /// typed by the model, so it is unaffected by not knowing one.</summary>
        public BodyContext AsTypeAgnostic(LateBodySink sink) =>
            new BodyContext(null, null, false, Props, SlotType, Fills, RegionHostProps, null, Root, null, sink);
    }
}
