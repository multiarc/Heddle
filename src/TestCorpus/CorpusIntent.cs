using System;
using System.Collections.Generic;
using System.Linq;

namespace Heddle.TestCorpus
{
    /// <summary>How the <b>build tier</b> must classify a corpus entry. These are exactly the buckets
    /// <c>CorpusDifferentialTests</c> already computed, promoted from prose comments plus a <c>HashSet</c> to a
    /// declared field so that the classification has an owner and a written reason.</summary>
    internal enum CorpusTier
    {
        /// <summary>A manifest entry with a bound (non-null) strategy and a generated entry class.</summary>
        Precompiles,

        /// <summary>Everything <see cref="Precompiles"/> is — a bound strategy and a generated entry class —
        /// <b>and</b> at least one recorded per-call-site fallback: a <c>PrecompiledRuntime.SiteFallback</c> field in
        /// the generated source, which is the build saying "this one call renders by compiling its own text at first
        /// render, and the rest of the template is emitted". Its populations are an extension declaring
        /// <c>[PrecompileUnsupported]</c> (<c>HED7033</c>) and a body the build had to emit type-agnostically that
        /// then reached one of the three shapes no type-agnostic emission reproduces.
        /// <para>Its own tier rather than a flavour of <see cref="Precompiles"/> because the difference is the
        /// headline capability of the binding seam: a template that gives up one call site and a template that gives
        /// up nothing classify identically in the manifest, so without this member the corpus would report the two as
        /// the same fact. Both tiers carry a bound entry class, so both are swept, rendered and byte-compared —
        /// <see cref="CorpusIntentRow.Bound"/> is what those gates read.</para></summary>
        PrecompilesWithSiteFallback,

        /// <summary>A HED7014 fallback-marker entry: present in the manifest with <c>strategy: null</c>, which is
        /// a different degrade from <see cref="FallsBackSafely"/> — the entry exists and routes to the dynamic
        /// path, rather than not existing at all.</summary>
        DegradesToMarker,

        /// <summary>No manifest entry at all — the whole template degraded to the dynamic tier, output-safely.</summary>
        FallsBackSafely,

        /// <summary>A deliberate front-end error the shared parser reports and the generator forwards as a build
        /// error, exactly as the dynamic backend would reject it.</summary>
        FrontEndError,
    }

    /// <summary>How the sweep may exercise an entry. Orthogonal to <see cref="CorpusTier"/> on purpose
    /// (prevents signal loss). For non-precompiling entries, forward-looking: what would happen if it precompiled.</summary>
    internal enum CorpusRender
    {
        /// <summary>Model-less and byte-compared against the dynamic reference inside the sweep. Verified: the entry
        /// renders on BOTH backends and the bytes agree.</summary>
        Standalone,

        /// <summary>The entry declares a model, so the path that matters is byte-pinned by the named family
        /// differential suite — a standalone render cannot type it. Its model-less path is still rendered and
        /// byte-compared alongside <see cref="Standalone"/>, because it is free coverage and because a row that
        /// stops rendering at all has changed into something this column no longer describes.</summary>
        WithModel,

        /// <summary>Not standalone-renderable at all — a fragment that is only meaningful when imported (a bare
        /// <c>@else</c> continuation), an entry whose own text is a deliberate parse error, or one that names a
        /// function only a host registration supplies. Resolved, never rendered by a shared harness.
        /// <para>This is the value that takes an entry out of byte-parity coverage, so it is the one that has to be
        /// earned: a precompiling entry declaring it is rendered anyway, and must genuinely fail to render.</para></summary>
        ResolveOnly,
    }

    /// <summary>One corpus entry's declared intent. One row per <c>.heddle</c> file, no exceptions — the two
    /// completeness gates assert both directions.</summary>
    internal sealed class CorpusIntentRow
    {
        public CorpusIntentRow(string name, CorpusTier tier, CorpusRender render, string why, bool bom = false)
        {
            Name = name;
            Tier = tier;
            Render = render;
            Why = why;
            Bom = bom;
        }

        /// <summary>The corpus file name (no directory).</summary>
        public string Name { get; }

        public CorpusTier Tier { get; }

        /// <summary>True for the two tiers that produce a bound entry class. Every gate that asks "does this
        /// template precompile" reads this rather than comparing against one member, so splitting the precompiling
        /// population by call-site fallback did not quietly narrow the render, sweep and observation gates to the
        /// rows that happen to give up nothing.</summary>
        public bool Bound => Tier == CorpusTier.Precompiles || Tier == CorpusTier.PrecompilesWithSiteFallback;

        public CorpusRender Render { get; }

        /// <summary>One line saying why this classification is correct. Asserted non-empty — a row without a reason
        /// is a rubber stamp, and the whole point of declaring intent is that contributing a template REQUIRES
        /// saying what it is for.</summary>
        public string Why { get; }

        /// <summary>This file intentionally carries a UTF-8 byte-order mark. Deliberate coverage, not an accident:
        /// hashing BOM-bearing templates correctly is a pinned behaviour, and until this flag existed a deliberate
        /// BOM and an accidental one were indistinguishable. Independent of line-ending pinning —
        /// <c>.gitattributes</c>' <c>eol=lf</c> governs newlines and says nothing whatsoever about byte-order
        /// marks.</summary>
        public bool Bom { get; }
    }

    /// <summary>
    /// The intent table: every shared-corpus entry declares, in one compile-checked place, how the build tier must
    /// classify it and how the sweep may exercise it.
    /// <para>A C# table rather than a filename convention (encodes one axis at most, unenforceable, mis-classifies on
    /// rename, cannot carry a reason), rather than an in-template header comment (it would change the bytes of the
    /// artifact whose whole value is byte fidelity), and rather than a TSV/JSON sidecar (needs a parser and a schema,
    /// loses compile-time checking). The repo's established pattern for shared test data is exactly this shape —
    /// <c>DiagnosticCorpusVectors</c>, <c>LineIndexVectors</c>, <c>PropDefaultConversionVectors</c> — and the
    /// classification being promoted was ALREADY a C# <c>HashSet</c> in <c>CorpusDifferentialTests</c>. This is a
    /// promotion of an existing artifact, not a new concept.</para>
    /// </summary>
    internal static class CorpusIntent
    {
        private static Dictionary<string, CorpusIntentRow> _byName;

        /// <summary>Every declared row, in the order authored (grouped by family, not sorted, so a family reads as a
        /// block).</summary>
        public static readonly IReadOnlyList<CorpusIntentRow> Rows = new[]
        {
            // Pure text plus @@ escapes collapses to raw, and pure static text precompiles.
            new CorpusIntentRow("at-escape.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Pure text plus @@ escapes collapses to a raw write, so the emitter binds it with no model."),
            new CorpusIntentRow("brace-misread.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Pure static text: the HED4005 brace-misread fixture's subject is a parse-time warning, not a construct the emitter must refuse."),
            new CorpusIntentRow("at-escape-comment-adjacent.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Model-less @@ escapes around @badge() calls under @if(true). It used to fall back because the emitter refused every native expression on the untyped tier before looking at it; the literal condition needs no model, so it now precompiles. Still pinned by its runtime golden (AtEscapeTests)."),

            // Definition libraries and import shells: they precompile like any plain definition library, and they
            // render standalone to the empty string on both tiers (verified, not assumed) because their whole body
            // is definitions that only produce output at an import site.
            new CorpusIntentRow("layout.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "A plain definition library; precompiles like the other libraries and renders empty on both tiers."),
            new CorpusIntentRow("ergo-import-empty.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Import fixture whose own output is empty on both tiers; carries no construct the emitter refuses."),
            new CorpusIntentRow("ergo-import-library.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The import library half of the composition pair; model-less, byte-pinned by CorpusRenderParityTests."),
            new CorpusIntentRow("ergo-import-composition.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Composes the import library; model-less byte parity is pinned by CorpusRenderParityTests."),
            new CorpusIntentRow("import-origin-badmember-lib.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The library half of the import-origin family: it is well-formed, so it precompiles even though the pages importing it are FrontEndError fixtures."),
            new CorpusIntentRow("regr-import-shell.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Hidden-token offset regression: the import library paired with regr-def-inner-comment; no definition layering, so it precompiles."),
            new CorpusIntentRow("regr-compose-shim.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Compose-nesting regression shim: as its own top-level document its @<< composes at offset 0, so it precompiles."),

            new CorpusIntentRow("branch-import-def.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Branch definition library: supported constructs only; renders empty standalone on both tiers."),
            new CorpusIntentRow("branch-import-else.heddle", CorpusTier.FallsBackSafely, CorpusRender.ResolveOnly,
                "A bare @else continuation fragment: it is only meaningful when imported into an opener's scope, and rendering it standalone throws 'branch terminal with no matching opener'. The build tier now reads that refusal from the shared branch scan and declines the body rather than precompiling past it, so the entry is a marker instead of a strategy."),
            new CorpusIntentRow("branching-flagship.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Flagship branch protocol shape; model-less and byte-identical across tiers."),
            new CorpusIntentRow("branching-interleaved.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Interleaved branch arms; model-less and byte-identical across tiers."),
            new CorpusIntentRow("branching-list-alternating.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Alternating list branch; renders empty with no model on both tiers."),
            new CorpusIntentRow("branching-nested.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Nested branch scopes; model-less and byte-identical across tiers."),
            new CorpusIntentRow("branching-partial-child.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The child half of the branch/partial pair; supported constructs only."),
            new CorpusIntentRow("branching-partial-parent.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The parent half of the branch/partial pair; byte parity also pinned by CorpusRenderParityTests."),

            new CorpusIntentRow("ergo-double-render.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Double-render warning fixture (W08); the warning is parse-time, the shape itself precompiles and is byte-pinned by CorpusRenderParityTests."),
            new CorpusIntentRow("ergo-for.heddle", CorpusTier.Precompiles, CorpusRender.WithModel,
                "Declares ':: ErgoForData', a Heddle.Tests model type, so a standalone dynamic render cannot resolve it; byte parity belongs to the for-family suite."),
            new CorpusIntentRow("ergo-trim-preamble.heddle", CorpusTier.Precompiles, CorpusRender.WithModel,
                "Declares ':: TestDataStructure'; the trim-directive goldens in Heddle.Tests own its bytes."),

            new CorpusIntentRow("profile-directive.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "@profile() directive flip; model-less and byte-pinned by CorpusRenderParityTests."),
            new CorpusIntentRow("profile-flagship.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Flagship profile-flip shape; model-less and byte-pinned by CorpusRenderParityTests."),
            new CorpusIntentRow("profile-partial-child.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The child half of the profile/partial pair; supported constructs only."),
            new CorpusIntentRow("profile-partial-parent.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The parent half of the profile/partial pair; byte parity also pinned by CorpusRenderParityTests."),
            new CorpusIntentRow("profile-resolver-default.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Resolver-default profile fixture; renders empty with no model on both tiers."),

            // Props / slot family. props-abstract-panel types dynamic; the other four write a bare short model name.
            new CorpusIntentRow("props-abstract-panel.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Abstract props panel with no declared model type, so it renders standalone on both tiers."),
            new CorpusIntentRow("props-defaults.heddle", CorpusTier.Precompiles, CorpusRender.WithModel,
                "Joined the precompiled set once the build tier stopped resolving model names by its own rule. Writes a bare ':: PropArticle', which a model-less standalone render cannot bind; the props differential suite owns its bytes."),
            new CorpusIntentRow("props-inherit.heddle", CorpusTier.Precompiles, CorpusRender.WithModel,
                "Props-defaults: a bare ':: PropArticle' short name the runtime binds through its global name index."),
            new CorpusIntentRow("slot-compose.heddle", CorpusTier.Precompiles, CorpusRender.WithModel,
                "Props-defaults: bare ':: PropArticle'; the slot/default-output suite owns its bytes."),
            new CorpusIntentRow("slot-picker.heddle", CorpusTier.Precompiles, CorpusRender.WithModel,
                "Bare ':: PropMenuOption'/':: PropMenu' short names; the slot suite owns its bytes."),

            new CorpusIntentRow("range-for.heddle", CorpusTier.Precompiles, CorpusRender.WithModel,
                "Range-for fixture: native-tier constructs only so it precompiles, but ':: ErgoForData' means byte parity is ForTests.RangeForFixture's."),

            new CorpusIntentRow("regr-def-inner-comment.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Hidden-token offset regression: a single-file definition with an inner-comment body; the enclosing-block trim fix lives in both backends, and the bytes are pinned by CorpusRenderParityTests."),
            new CorpusIntentRow("shaper-clamp-imported.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The library half of the clamp-drift pair; a zero-output directive, so it renders empty on both tiers."),
            new CorpusIntentRow("shaper-clamp-overshoot.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Clamp drift: an indented last-line @<< whose re-based chain overshoots the widened-away import line. Precompiles only since the clamp fix; before it the emitter threw and silently degraded."),
            new CorpusIntentRow("optimized-document.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The optimized-document flagship; model-less, byte-pinned by CorpusRenderParityTests, and BOM-bearing.",
                bom: true),

            new CorpusIntentRow("streaming-large.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Streaming fixture: pure static text, so it precompiles on the dynamic tier and renders identically."),
            new CorpusIntentRow("streaming-unicode.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Streaming fixture: static plus dynamic @(Name)/@(City) against a dynamic model, so it precompiles and renders identically."),

            new CorpusIntentRow("trycompile-parity-child.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The static child of the TryCompile-parity pair; supported constructs only."),
            new CorpusIntentRow("trycompile-parity-parent.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "A parent with a @partial call site of the static child; supported constructs only."),
            new CorpusIntentRow("trycompile-parity-typed.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "A typed @(Name) document against a dynamic model; renders empty standalone on both tiers."),

            new CorpusIntentRow("branching-out-projection.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "An @out projection inside a branch. It refused because its definition declares no model type and the emitter typed that body 'object', where the engine types it by the value each call site passes; typed the engine's way the body's branches and projection are all ordinary emissions."),
            new CorpusIntentRow("context-lint-corpus.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "The HTML-context lint corpus: its subject is parse-time HED4xxx classification, and it needs the lint suite's host setup to render."),
            new CorpusIntentRow("ctx-encoding.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Context-encoding golden fixture: every call is a BODILESS step-back encoder (@url/@attr/@js), which the emitter now binds from pinned knowledge — the hook re-types only a default body these calls do not have. ContextEncodingFallbackTests proves tier parity; the bodied form still falls back."),
            new CorpusIntentRow("ctx-encoding-bodied.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The bodied twin of ctx-encoding, flipped. A step-back encoder's hook re-types its default body against the CALLER's scope and does nothing else — one hook body shared verbatim by nine built-ins — and that role is now a row of the shared table, rather than a four-name list the emitter carried privately and had five names missing from. The body is emitted in the enclosing model's context, exactly as an @if body is."),
            new CorpusIntentRow("ext-bodied-custom.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "A bodied call to a REFERENCED third-party extension whose InitStart has the step-back shape — the case the whole binding seam exists for, and the one the build could not serve at all. It is no longer a question the build answers: the extension's own InitStart runs inside the consumer's assembly at static-init, the body is emitted with no model cast, and the hook chooses that body's typing. Nothing opts in, no name is listed, and the HED7015 warning it used to carry is simply not true of this build any more."),
            new CorpusIntentRow("ext-bodied-unemittable.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The boundary beside it, moved by the same change: a bodied call to a referenced extension whose hook types the body by the call VALUE. That was a role the emitter had no emission for, and a role is no longer what decides — the body carries no model cast to be wrong, so there is nothing left for the build to be unable to emit. Its bytes are pinned against the dynamic tier by the sweep like every other row here."),
            new CorpusIntentRow("ext-site-fallback.heddle", CorpusTier.PrecompilesWithSiteFallback,
                CorpusRender.Standalone,
                "The corpus's member of the per-call-site fallback tier, and the only row that makes that tier a gate rather than a declaration. @scanner declares [PrecompileUnsupported] because its InitStart walks the enclosing document through InitContext.ParseContext, which no call site can carry — so the build writes a PrecompiledRuntime.SiteFallback for that one call, reports HED7033 quoting the extension author's own sentence, and emits the rest of the document as an ordinary precompiled entry class. Model-less and standalone, so the sweep byte-compares both tiers every run: what the substitute renders by compiling its own text is what the dynamic tier renders."),
            new CorpusIntentRow("dynamic-recursion.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "Its embedded C# now compiles as fragments — under a FullCSharp build the whole document precompiles byte-identically (EmbeddedCSharpFragmentTests) — but this sweep builds with the default Native mode, where 'embedded C# outside FullCSharp mode' degrades it, exactly as the engine refuses the same template without FullCSharp options.",
                bom: true),
            new CorpusIntentRow("empty-override.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Definition override layering, which the emitter no longer refuses. It never resolved layers itself: it asks the same ParseContext the engine asks, and the parser has already put the layer each call site sees in it, so an override arrives materialized and emits as an ordinary definition call. Renders model-less on both tiers and is still pinned by HeddleTemplateTests.",
                bom: true),
            new CorpusIntentRow("def-layering.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The layering shapes in one model-less document: a full override calling the layer below it, props declared on two layers, a three-deep chain where each layer reaches only the one under it, an override declared inside another definition's body, and caller content spliced separately at each layer. Model-less and standalone so the sweep byte-compares it on both tiers every run — the shapes that were refused wholesale until the emitter stopped believing it resolved definitions flatly."),
            new CorpusIntentRow("expr-flagship.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "Native-expression flagship: HED1004 requires a typed model, so it cannot render model-less; NativeExpressionGoldenTests owns its bytes."),
            new CorpusIntentRow("expr-functions.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "Native-expression function fixture: typed-model-only (HED1004), same owner as expr-flagship."),
            new CorpusIntentRow("partial.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "An HTML row fragment, named for what it is rather than for the @partial extension its Why used to blame. Its one blocker was @money(Cost){{@(Locale)}} — a bodied step-back encoder, the same refusal ctx-encoding-bodied pins — and @money was not even in the private four-name list that would have let its bodiless form bind. Both facts fell out together when the roles became an observation.",
                bom: true),
            new CorpusIntentRow("props-card.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "Props card fixture whose model member the emitter cannot type; PropsGoldenTests owns its bytes."),
            new CorpusIntentRow("raw.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "@raw over a host-provided value; needs the raw suite's host setup, and the emitter refuses the construct.",
                bom: true),
            new CorpusIntentRow("recursion.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "Its embedded C# now compiles as fragments — under a FullCSharp build the whole document precompiles byte-identically (EmbeddedCSharpFragmentTests) — but this sweep builds with the default Native mode, where 'embedded C# outside FullCSharp mode' degrades it, exactly as the engine refuses the same template without FullCSharp options.",
                bom: true),
            new CorpusIntentRow("regr-import-multiline-override.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The cross-file override page of the hidden-token regression. Layering an IMPORTED definition is layering like any other — the import is expanded into this document before the parse that builds the layers — so it moved with the rest of them; MultilineOverrideOffsetRegressionTests still pins the runtime bytes."),
            new CorpusIntentRow("template.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "The original flagship document. Under this sweep's default Native build the mode gate degrades its embedded C#; under FullCSharp the blocker is its escape-bearing @using body ('Heddle@{.}@Tests.Data'), which the engine renders to a namespace and the emitter still reads raw. HeddleTemplateTests owns its bytes.",
                bom: true),
            new CorpusIntentRow("tuple_array.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "Its first refusal is the bodied unnamed carrier (@(...){{...}}), reached before any of its tuple C# is even asked about; HeddleTemplateTests owns its bytes."),
            new CorpusIntentRow("vc-test.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "The double-render (W08) subject document. Five of its widgets are full overrides, which was its only blocker; with layering emitted it precompiles, and it renders model-less on both tiers."),
            new CorpusIntentRow("wierd-whitespace.heddle", CorpusTier.FallsBackSafely, CorpusRender.WithModel,
                "Whitespace-torture document whose first refusal is the bodied unnamed carrier (@(  ){{...}}), reached before its embedded C#; HeddleTemplateTests owns its bytes.",
                bom: true),

            new CorpusIntentRow("fn-late-bound.heddle", CorpusTier.Precompiles, CorpusRender.ResolveOnly,
                "Was the corpus's marker entry until late binding arrived: it calls a function resolvable from neither the default table nor any referenced export, which is now emitted as a PrecompiledFunctionSite resolved once at first render through the engine's own ranker instead of costing the file its tier. Renders only against a host that registers the delegate — with none registered BOTH tiers refuse the same way, which is what earns its ResolveOnly row and is asserted rather than assumed; LateBoundFunctionTests owns its bytes and its ranking."),
            new CorpusIntentRow("fn-unresolvable-marker.heddle", CorpusTier.DegradesToMarker, CorpusRender.ResolveOnly,
                "The marker entry, kept deliberately so the tier still has a member after late binding took the previous one. Its outer call's ARGUMENT is an expression over a second un-bindable name, whose return type is exactly what no build-time answer exists for — so the argument has no static type to rank the outer call against, and inventing one is the one thing a late-bound site must not do (it would pick an overload the engine never picks). Still the only construct yielding a manifest row with a null strategy instead of no row at all; UnresolvableFunctionTests owns its classification and its diagnostic."),

            // These entries assert the diagnostic's IDENTITY, not offsets into a hand-counted string, which is why
            // they are corpus entries at all; position probes stay inline in their own tests.

            new CorpusIntentRow("ergo-import-broken.heddle", CorpusTier.FrontEndError, CorpusRender.ResolveOnly,
                "Imports a target that does not resolve; the front end errors and the generator forwards it."),
            new CorpusIntentRow("import-origin-a.heddle", CorpusTier.FrontEndError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture: the error must be attributed to this file, not to the library."),
            new CorpusIntentRow("import-origin-b.heddle", CorpusTier.FrontEndError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture, second hop of the chain."),
            new CorpusIntentRow("import-origin-broken.heddle", CorpusTier.FrontEndError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture: the deliberately broken origin."),
            new CorpusIntentRow("import-origin-c.heddle", CorpusTier.FrontEndError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture, third hop of the chain."),

            // Chains. The corpus carried none at all while the build refused them, so these are the shared home
            // for the shape rather than an adaptation of one.
            new CorpusIntentRow("chain-output.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Multi-item output chains — two definitions around a definition producer, and a definition around a registered function — model-less, so both tiers render them and the bytes are compared."),
            new CorpusIntentRow("chain-parameter.heddle", CorpusTier.Precompiles, CorpusRender.Standalone,
                "Multi-item chains in call-parameter position (@a(b():c())), the grammar's third call alternative; model-less and byte-compared like its output-position sibling."),
        };

        private static Dictionary<string, CorpusIntentRow> ByName
        {
            get
            {
                if (_byName == null)
                {
                    var map = new Dictionary<string, CorpusIntentRow>(StringComparer.Ordinal);
                    foreach (var row in Rows)
                    {
                        if (map.ContainsKey(row.Name))
                            throw new InvalidOperationException(
                                "Duplicate corpus intent row: " + row.Name + ". One row per .heddle file, exactly.");
                        map.Add(row.Name, row);
                    }

                    _byName = map;
                }

                return _byName;
            }
        }

        /// <summary>The declared intent for one corpus file. Throws naming the file on a miss — the gate that
        /// makes "contributing a template requires declaring what it is for" a mechanism rather than a request.
        /// </summary>
        public static CorpusIntentRow For(string name)
        {
            if (ByName.TryGetValue(name, out var row))
                return row;
            throw new InvalidOperationException(
                "No corpus intent row declares '" + name + "'. Every file in src/Heddle.Tests/TestTemplate/**.heddle " +
                "needs exactly one row in src/TestCorpus/CorpusIntent.cs, with a Tier, a Render and a non-empty Why.");
        }

        public static bool TryGet(string name, out CorpusIntentRow row) => ByName.TryGetValue(name, out row);

        /// <summary>Every declared file name, ordinal-sorted. The declared half of the completeness gates.</summary>
        public static IReadOnlyList<string> DeclaredNames() =>
            Rows.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Every declared file name in one tier, ordinal-sorted. The right-hand side of the per-tier set
        /// equality.</summary>
        public static IReadOnlyList<string> NamesWithTier(CorpusTier tier) =>
            Rows.Where(r => r.Tier == tier).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Every declared file name that produces a bound entry class, in either precompiling tier,
        /// ordinal-sorted. The right-hand side of the gates that ask about the manifest alone, which cannot see
        /// which of the two a row is.</summary>
        public static IReadOnlyList<string> BoundNames() =>
            Rows.Where(r => r.Bound).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Every declared file name with one render disposition, ordinal-sorted.</summary>
        public static IReadOnlyList<string> NamesWithRender(CorpusRender render) =>
            Rows.Where(r => r.Render == render).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>The names the encoding gate expects to carry a UTF-8 BOM, ordinal-sorted.</summary>
        public static IReadOnlyList<string> BomNames() =>
            Rows.Where(r => r.Bom).Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        /// <summary>Formats a symmetric difference as a review-ready message. Making the gate green then requires
        /// naming the specific file whose classification changed, which is exactly the review artifact a
        /// bare count was trying to force and could never produce ("expected 40, got 39" names nothing).</summary>
        public static string Describe(string what, IEnumerable<string> declared, IEnumerable<string> observed)
        {
            var d = new SortedSet<string>(declared, StringComparer.Ordinal);
            var o = new SortedSet<string>(observed, StringComparer.Ordinal);
            var missing = d.Except(o).ToList();     // declared but not observed
            var unexpected = o.Except(d).ToList();  // observed but not declared
            return what + " drifted from the declared intent table (src/TestCorpus/CorpusIntent.cs)."
                   + "\n  Declared but NOT observed (" + missing.Count + "): "
                   + (missing.Count == 0 ? "(none)" : string.Join(", ", missing))
                   + "\n  Observed but NOT declared (" + unexpected.Count + "): "
                   + (unexpected.Count == 0 ? "(none)" : string.Join(", ", unexpected))
                   + "\n  Fix by changing the code, or by changing that file's row and saying why in its Why.";
        }
    }
}
