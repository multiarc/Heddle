using System;
using Heddle.Data;

namespace Heddle.Precompiled
{
    /// <summary>
    /// Everything the engine's compiler knows at one extension call site, reduced to what a generated static field
    /// initializer can carry. <see cref="PrecompiledRuntime.Init"/> rebuilds the engine's
    /// <c>InitContext</c>/<c>CompileScope</c>/<c>ParseContext</c> from it and runs the extension's real
    /// <c>InitStart</c>, so the hook decides its own compile-time state instead of the build predicting it.
    /// <para>Not intended for hand-written use. Every member is settable so a generated initializer can write it as
    /// one object initializer; nothing mutates it after that except <see cref="Fault"/>, which
    /// <see cref="PrecompiledRuntime.Init"/> writes when the hook did not succeed.</para>
    /// <para><b>Types are spelled as <see cref="Type"/>, and <c>null</c> means <c>dynamic</c></b> — the engine's
    /// <see cref="ExType.Dynamic"/>, not the absence of a type. That is the one collapse the carriage makes: an
    /// <see cref="ExType"/> is a <see cref="Type"/> plus a dynamic flag, and generated code cannot spell the
    /// flag.</para>
    /// </summary>
    public sealed class PrecompiledInitSite
    {
        /// <summary>The registered extension name the call names; empty for an unnamed carrier.</summary>
        public string ExtensionName { get; set; }

        /// <summary>The call's start offset in the enclosing template document.</summary>
        public int PositionStart { get; set; }

        /// <summary>The call's length; with <see cref="PositionStart"/>, the <c>BlockPosition</c> the engine gives
        /// this call.</summary>
        public int PositionLength { get; set; }

        /// <summary>The invoked <b>definition</b>'s declaration start offset, for
        /// <see cref="PrecompiledRuntime.InitDefinition"/>. The engine positions both definition carriers at the
        /// declaration, not at the call (<c>CompileFromDefenition</c>), and that position is what a compile fault or
        /// an <c>@out</c> without a source item is reported against.</summary>
        public int DefinitionPositionStart { get; set; }

        /// <summary>The invoked definition's declaration length; see <see cref="DefinitionPositionStart"/>.</summary>
        public int DefinitionPositionLength { get; set; }

        /// <summary>The call's own source text — the document span at <see cref="PositionStart"/>. Read only when a
        /// hook faults, by the substitute this class's <see cref="Fault"/> path installs, which compiles it as its
        /// own document at first render.</summary>
        public string SourceText { get; set; }

        /// <summary>The call's body: raw text, shaped text, locals flag, and the typing the build assumed.
        /// For a definition invocation this is the <b>caller content</b>, and
        /// <see cref="DefinitionBody"/> carries the definition's own body.</summary>
        public PrecompiledInitBody Body { get; set; }

        /// <summary>The definition's own body, for <see cref="PrecompiledRuntime.InitDefinition"/> only;
        /// <c>null</c> for a plain extension call.</summary>
        public PrecompiledInitBody DefinitionBody { get; set; }

        /// <summary>The <c>dataType</c> argument the engine passes <c>InitStart</c> — the model type flowing into
        /// this call. <c>null</c> means <c>dynamic</c>.</summary>
        public Type DataType { get; set; }

        /// <summary>The <c>chainedType</c> argument the engine passes <c>InitStart</c> — the type the producer to
        /// this call's right returns. <c>null</c> means <c>dynamic</c>.</summary>
        public Type ChainedType { get; set; }

        /// <summary>The <c>parent</c> argument the engine passes <c>InitStart</c>, which is the enclosing
        /// <c>CompileScope.ScopeType</c>. <c>null</c> means <c>dynamic</c>.</summary>
        public Type ParentType { get; set; }

        /// <summary>The enclosing scope's model type (<c>CompileScope.ScopeType</c>). <c>null</c> means
        /// <c>dynamic</c>.</summary>
        public Type ModelType { get; set; }

        /// <summary>The document's root model type (<c>CompileScope.RootScopeType</c>). <c>null</c> means
        /// <c>dynamic</c>.</summary>
        public Type RootModelType { get; set; }

        /// <summary>The active slot parameter type (<c>CompileContext.SlotParameterType</c>) — non-null only inside
        /// the body of a definition declaring <c>out:: T</c>, which is what puts <c>@out</c> in slot mode.</summary>
        public Type SlotType { get; set; }

        /// <summary>The slot parameter type the invoked <b>definition</b> declares (<c>out:: T</c>), for
        /// <see cref="PrecompiledRuntime.InitDefinition"/> only. It is what puts the invocation in slot mode and what
        /// the engine installs on the compile context while the definition body compiles — a different value from
        /// <see cref="SlotType"/>, which is whatever was already active at the call.</summary>
        public Type DefinitionSlotType { get; set; }

        /// <summary>The effective output profile for this call.</summary>
        public OutputProfile OutputProfile { get; set; }

        /// <summary>The expression tier the enclosing compile runs under.</summary>
        public ExpressionMode ExpressionMode { get; set; }

        /// <summary>The enclosing compile's <c>TemplateOptions.TrimDirectiveLines</c>.</summary>
        public bool TrimDirectiveLines { get; set; }

        /// <summary>The enclosing compile's <c>TemplateOptions.MaxRecursionCount</c>, which is where
        /// <c>DefinitionBaseExtension</c>'s recursion guard reads its limit from.</summary>
        public int MaxRecursionCount { get; set; }

        /// <summary>The <c>@using</c> namespace set in scope at this call, which the engine's type resolution
        /// reads. <c>null</c> is treated as empty.</summary>
        public string[] Namespaces { get; set; }

        /// <summary>The call's positional parameter shape.</summary>
        public PrecompiledCallShape CallShape { get; set; }

        /// <summary>Whether the call also passes named prop arguments — orthogonal to
        /// <see cref="CallShape"/>, which reports them only when no positional arm matched.</summary>
        public bool HasPropArguments { get; set; }

        /// <summary>Whether the call's model path is <c>::</c>-rooted (<c>CallParameter.RootReference</c>).</summary>
        public bool RootReference { get; set; }

        /// <summary>Whether this is a non-leading item of its chain (<c>OutputItem.IsChainedConsumer</c>), which is
        /// what arms <c>@out</c>'s composed-projection guard.</summary>
        public bool IsChainedConsumer { get; set; }

        /// <summary>Whether the chain has a producer to this call's right — the compiler's own
        /// <c>hasProducerToRight</c>, which together with a bodiless call is what makes a definition invocation
        /// take its content from the chained channel.</summary>
        public bool HasProducerToRight { get; set; }

        /// <summary>Whether the call sits inside a definition body (<c>ParseContext.InDefintionContext</c>), which
        /// selects which of <c>@out</c>'s two "value without a slot" sentences the engine writes.</summary>
        public bool InsideDefinition { get; set; }

        /// <summary>The member reads of a <b>type-agnostic</b> body — one whose model type the build could not
        /// resolve, because the extension hosting it decides that type in its own hook.
        /// <see cref="PrecompiledRuntime.Init"/> binds every one of them against the type the hook answered with,
        /// so the reads are the engine's rather than a guess. <c>null</c> for a body the build typed itself.</summary>
        public PrecompiledLateAccessor[] BodyAccessors { get; set; }

        /// <summary>Call sites nested inside this call's type-agnostic body, whose own <c>dataType</c> is a
        /// function of the model this hook hands the body. <see cref="PrecompiledRuntime.Init"/> writes that model
        /// onto each of them before their own initializers run, which is what makes the typing cascade rather than
        /// sit as a flat sibling. <c>null</c> when the body is typed by the build or hosts no further calls.</summary>
        public PrecompiledInitSite[] Dependents { get; set; }

        /// <summary>For a site nested in a type-agnostic body: the member path whose resolved type is this call's
        /// <c>dataType</c>. The path is known at build; the type it resolves to is not, because it starts at the
        /// model the enclosing hook chooses. <see cref="PrecompiledRuntime.Init"/> walks it with the engine's own
        /// member resolution and writes <see cref="DataType"/>. <c>null</c> for every other site.</summary>
        public string[] DataTypePath { get; set; }

        /// <summary>The model type the hook actually handed this call's body compile, published by
        /// <see cref="PrecompiledRuntime.Init"/>. <c>null</c> means the hook answered <c>dynamic</c>, or the hook has
        /// not run yet.</summary>
        public Type ResolvedBodyDataType { get; set; }

        /// <summary>The chained type the hook handed this call's body compile; see
        /// <see cref="ResolvedBodyDataType"/>.</summary>
        public Type ResolvedBodyChainedType { get; set; }

        /// <summary>What <see cref="PrecompiledRuntime.Init"/> recorded when the hook did not succeed; <c>null</c>
        /// on the success path. Never thrown — see <see cref="PrecompiledInitFault"/> for why.</summary>
        public PrecompiledInitFault Fault { get; set; }
    }
}
