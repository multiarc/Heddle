using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;

namespace Heddle.TestCorpus
{
    /// <summary>How the <b>build tier</b> (the compiled-form record path) must classify a corpus entry.
    /// The engine's verdict under the row's mode is the whole law: a template the engine compiles is in
    /// the artifact, and a template the engine refuses is a build error — nothing else refuses.</summary>
    internal enum CorpusTier
    {
        /// <summary>The engine compiles the row under its mode, and the record carries no refusal sites.</summary>
        Compiles,

        /// <summary>The engine compiles the row, and the record carries at least one refusal site whose
        /// classes equal the row's <see cref="CorpusIntentRow.Refusals"/>: the site alone falls back, the
        /// rest of the template stays precompiled.</summary>
        CompilesWithRefusal,

        /// <summary>The engine refuses the row under its mode, so the build fails it too. No artifact,
        /// no refusal sites — a refusal site is a per-site degrade inside a compiled template, not a
        /// whole-template verdict.</summary>
        EngineError,
    }

    /// <summary>The three refusal classes (P1-R8). Mirrors <c>Heddle.Precompiled.PrecompiledRefusalClass</c>
    /// member for member; the gate compares the two by name, so a rename on either side fails loudly
    /// instead of drifting silently.</summary>
    internal enum RefusalClass
    {
        /// <summary>The bound extension type declares <c>[PrecompileUnsupported]</c> (read off the live
        /// type at build).</summary>
        UnsupportedExtension,

        /// <summary>A value the engine types by reflection enumeration order.</summary>
        ReflectionOrderValue,

        /// <summary>A bodied or chained consumer over an unbindable call.</summary>
        UnbindableCallTyping,
    }

    /// <summary>How the sweep may exercise an entry. Orthogonal to <see cref="CorpusTier"/> on purpose
    /// (prevents signal loss). Unchanged by the compiled-form rewrite: what renders a row is still a
    /// property of the row, not of the tier.</summary>
    internal enum CorpusRender
    {
        /// <summary>Model-less and byte-compared against the dynamic reference inside the sweep. Verified: the entry
        /// renders on BOTH backends and the bytes agree.</summary>
        Standalone,

        /// <summary>The entry needs its <c>CorpusModels</c> model to compile, so the path that matters is the
        /// typed one. Its model-less path is still rendered and byte-compared alongside
        /// <see cref="Standalone"/>, because it is free coverage and because a row that stops rendering at
        /// all has changed into something this column no longer describes.</summary>
        WithModel,

        /// <summary>Not standalone-renderable at all — a fragment that is only meaningful when imported (a bare
        /// <c>@else</c> continuation), an entry whose own text is a deliberate engine error, one that names a
        /// function only a host registration supplies, or one the engine refuses under the row's mode.
        /// Resolved, never rendered by a shared harness.
        /// <para>This is the value that takes an entry out of byte-parity coverage, so it is the one that has to be
        /// earned: a compiling entry declaring it is rendered anyway, and must genuinely fail to render.</para></summary>
        ResolveOnly,
    }

    /// <summary>One corpus entry's declared intent. One row per <c>.heddle</c> file, no exceptions — the two
    /// completeness gates assert both directions.</summary>
    internal sealed class CorpusIntentRow
    {
        public CorpusIntentRow(string name, CorpusTier tier, CorpusRender render, string why, bool bom = false,
            ExpressionMode mode = ExpressionMode.Native, params RefusalClass[] refusals)
        {
            Name = name;
            Tier = tier;
            Render = render;
            Why = why;
            Bom = bom;
            Mode = mode;
            Refusals = refusals ?? new RefusalClass[0];
        }

        /// <summary>The corpus file name (no directory).</summary>
        public string Name { get; }

        public CorpusTier Tier { get; }

        /// <summary>True for the two tiers that produce an artifact. Every gate that asks "does this
        /// template precompile" reads this rather than comparing against one member, so splitting the
        /// compiling population by refusal site did not quietly narrow the sweep gates to the rows that
        /// happen to give up nothing.</summary>
        public bool Bound => Tier == CorpusTier.Compiles || Tier == CorpusTier.CompilesWithRefusal;

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

        /// <summary>The expression mode the row is classified under. Native unless the engine needs FullCSharp
        /// to compile the row at all (embedded C#): such rows carry the mode that saves them, and rows whose
        /// C# no mode saves are <see cref="CorpusTier.EngineError"/> under Native instead.</summary>
        public ExpressionMode Mode { get; }

        /// <summary>The refusal classes the row's record must carry, as a set. Empty for every row that
        /// compiles clean; the gate asserts set equality against the record's <c>RefusalSites</c>.</summary>
        public IReadOnlyList<RefusalClass> Refusals { get; }
    }

    /// <summary>
    /// The intent table: every shared-corpus entry declares, in one compile-checked place, how the build tier must
    /// classify it and how the sweep may exercise it.
    /// <para>A C# table rather than a filename convention (encodes one axis at most, unenforceable, mis-classifies on
    /// rename, cannot carry a reason), rather than an in-template header comment (it would change the bytes of the
    /// artifact whose whole value is byte fidelity), and rather than a TSV/JSON sidecar (needs a parser and a schema,
    /// loses compile-time checking). The repo's established pattern for shared test data is exactly this shape —
    /// <c>DiagnosticCorpusVectors</c>, <c>LineIndexVectors</c>, <c>PropDefaultConversionVectors</c>.</para>
    /// </summary>
    internal static class CorpusIntent
    {
        private static Dictionary<string, CorpusIntentRow> _byName;

        /// <summary>Every declared row, in the order authored (grouped by family, not sorted, so a family reads as a
        /// block).</summary>
        public static readonly IReadOnlyList<CorpusIntentRow> Rows = new[]
        {
            // Pure text plus @@ escapes collapses to raw: no call sites, nothing to refuse, dynamic root.
            new CorpusIntentRow("at-escape.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Pure text plus @@ escapes collapses to a raw write, so the record carries no call sites at all."),
            new CorpusIntentRow("brace-misread.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Pure static text: the HED4005 brace-misread fixture's subject is a parse-time warning, not a construct the record must refuse."),
            new CorpusIntentRow("at-escape-comment-adjacent.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Model-less @@ escapes around @badge() calls under @if(true); the literal condition needs no model, so the record is clean. Still pinned by its runtime golden (AtEscapeTests)."),

            // Definition libraries and import shells: they compile like any plain definition library, and they
            // render standalone to the empty string on both tiers (verified, not assumed) because their whole body
            // is definitions that only produce output at an import site.
            new CorpusIntentRow("layout.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "A plain definition library; compiles like the other libraries and renders empty on both tiers."),
            new CorpusIntentRow("ergo-import-empty.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Import fixture whose own output is empty on both tiers; carries no construct the record refuses."),
            new CorpusIntentRow("ergo-import-library.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The import library half of the composition pair; model-less."),
            new CorpusIntentRow("ergo-import-composition.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Composes the import library; imports resolve from the corpus directory on both tiers."),
            new CorpusIntentRow("import-origin-badmember-lib.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The library half of the import-origin family: it is well-formed, so it compiles even though the pages importing it are EngineError fixtures."),
            new CorpusIntentRow("regr-import-shell.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Hidden-token offset regression: the import library paired with regr-def-inner-comment; no definition layering, so it compiles."),
            new CorpusIntentRow("regr-compose-shim.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Compose-nesting regression shim: as its own top-level document its @<< composes at offset 0, so it compiles."),

            new CorpusIntentRow("branch-import-def.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Branch definition library: supported constructs only; renders empty standalone on both tiers."),
            new CorpusIntentRow("branch-import-else.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "A bare @else continuation fragment: it is only meaningful when imported into an opener's scope, and compiling it standalone throws HED3003 on both tiers. The build fails rather than emitting past it, so the entry resolves but never renders."),
            new CorpusIntentRow("branching-flagship.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Flagship branch protocol shape; its reads late-bind under a dynamic root, so it compiles model-less and byte-identical across tiers."),
            new CorpusIntentRow("branching-interleaved.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Interleaved branch arms; model-less and byte-identical across tiers under a dynamic root."),
            new CorpusIntentRow("branching-list-alternating.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Alternating list branch; renders empty with no model on both tiers."),
            new CorpusIntentRow("branching-nested.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Nested branch scopes; model-less and byte-identical across tiers under a dynamic root."),
            new CorpusIntentRow("branching-partial-child.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The child half of the branch/partial pair; supported constructs only."),
            new CorpusIntentRow("branching-partial-parent.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The parent half of the branch/partial pair; a @partial call site of the static child."),

            new CorpusIntentRow("ergo-double-render.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Double-render warning fixture (W08); the warning is parse-time, the shape itself compiles."),
            new CorpusIntentRow("ergo-for.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Declares @model(){{ErgoForData}}, and the @for sugar reads type against it; byte parity belongs to the for-family suite."),
            new CorpusIntentRow("ergo-trim-preamble.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Declares @model(){{TestDataStructure}}; the trim-directive goldens in Heddle.Tests own its bytes."),

            new CorpusIntentRow("profile-directive.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "@profile() directive flip; model-less, its reads late-bind under a dynamic root."),
            new CorpusIntentRow("profile-flagship.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Flagship profile-flip shape; model-less and byte-identical across tiers under a dynamic root."),
            new CorpusIntentRow("profile-partial-child.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The child half of the profile/partial pair; supported constructs only."),
            new CorpusIntentRow("profile-partial-parent.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The parent half of the profile/partial pair; model-less under a dynamic root."),
            new CorpusIntentRow("profile-resolver-default.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Resolver-default profile fixture; renders empty with no model on both tiers."),

            // Props / slot family. props-abstract-panel types dynamic; the rest read a typed root: bare short
            // model names (:: PropArticle and friends) resolve through the global name index, while the
            // call-site root reads (Article, Site, Menu) type against PropRoot.
            new CorpusIntentRow("props-abstract-panel.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Abstract props panel with no declared model type, so it renders standalone on both tiers."),
            new CorpusIntentRow("props-defaults.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Writes a bare ':: PropArticle', which a model-less compile cannot root; the props differential suite owns its bytes."),
            new CorpusIntentRow("props-inherit.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Bare ':: PropArticle' short name the runtime binds through its global name index; the root reads need PropRoot."),
            new CorpusIntentRow("slot-compose.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Bare ':: PropArticle'; the slot/default-output suite owns its bytes."),
            new CorpusIntentRow("slot-picker.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Bare ':: PropMenuOption'/':: PropMenu' short names; the slot suite owns its bytes."),

            new CorpusIntentRow("range-for.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Range-for fixture declaring @model(){{ErgoForData}}; byte parity is ForTests.RangeForFixture's."),

            new CorpusIntentRow("regr-def-inner-comment.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Hidden-token offset regression: a single-file definition with an inner-comment body; the enclosing-block trim fix lives in both backends."),
            new CorpusIntentRow("shaper-clamp-imported.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The library half of the clamp-drift pair; a zero-output directive, so it renders empty on both tiers."),
            new CorpusIntentRow("shaper-clamp-overshoot.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Clamp drift: an indented last-line @<< whose re-based chain overshoots the widened-away import line; ordinary emission."),
            new CorpusIntentRow("optimized-document.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The optimized-document flagship; model-less and BOM-bearing.",
                bom: true),

            new CorpusIntentRow("streaming-large.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Streaming fixture: pure static text, so it compiles and renders identically."),
            new CorpusIntentRow("streaming-unicode.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Streaming fixture: static plus dynamic @(Name)/@(City), which late-bind under a dynamic root, so it compiles and renders identically."),

            new CorpusIntentRow("trycompile-parity-child.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The static child of the TryCompile-parity pair; supported constructs only."),
            new CorpusIntentRow("trycompile-parity-parent.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "A parent with a @partial call site of the static child; supported constructs only."),
            new CorpusIntentRow("trycompile-parity-typed.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "A typed @(Name) document; renders empty standalone on both tiers under a dynamic root."),

            new CorpusIntentRow("branching-out-projection.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "An @out projection inside a branch over a @model(){{dynamic}} root: the body's branches and projection are all ordinary emissions."),
            new CorpusIntentRow("context-lint-corpus.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "The HTML-context lint corpus: its @(X)/FullUrl/Id/Count/Label reads type against HtmlContextLintTests.LintModel, the same model the lint goldens render with."),
            new CorpusIntentRow("ctx-encoding.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Context-encoding golden fixture: every call is a BODILESS step-back encoder (@url/@attr/@js), which the record binds from pinned knowledge — the hook re-types only a default body these calls do not have."),
            new CorpusIntentRow("ctx-encoding-bodied.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The bodied twin of ctx-encoding. A step-back encoder's hook re-types its default body against the CALLER's scope and does nothing else, and the body is recorded in the enclosing dynamic context, exactly as an @if body is."),
            new CorpusIntentRow("ext-bodied-custom.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "A bodied call to a REFERENCED third-party extension whose InitStart has the step-back shape — the case the whole binding seam exists for. The extension's own InitStart runs inside the consumer's assembly at static-init, the body carries no model cast, the hook chooses that body's typing, and the record carries no refusal."),
            new CorpusIntentRow("ext-bodied-unemittable.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The boundary beside it: a bodied call to a referenced extension whose hook types the body by the call VALUE. The body carries no model cast to be wrong, so there is nothing left the record cannot carry — no refusal, bytes pinned against the dynamic tier by the sweep like every other row here."),
            new CorpusIntentRow("inert-body.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The three ways a call body compiles to no processors — static text alone, a directive alone, and an empty body — beside the two forms that must stay distinguished from them: a bodiless call, a body holding a real call, and the unnamed carrier. The carrier renders its model and not the body text on both tiers."),
            new CorpusIntentRow("ext-site-fallback.heddle", CorpusTier.CompilesWithRefusal,
                CorpusRender.Standalone,
                "The corpus's one per-site refusal. @scanner declares [PrecompileUnsupported] because its InitStart walks the enclosing document through InitContext.ParseContext, which no call site can carry — so the record carries one UnsupportedExtension site quoting the author's sentence, and the rest of the document stays precompiled. Model-less and standalone, so the sweep byte-compares both tiers every run.",
                refusals: RefusalClass.UnsupportedExtension),
            new CorpusIntentRow("dynamic-recursion.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Embedded-C# recursion flagship with a dynamic root: the engine refuses its C# under Native (positions 139, 781), and compiles it under FullCSharp — EmbeddedCSharpFragmentTests pins the byte-identical precompile, which is why this row carries the mode.",
                bom: true, mode: ExpressionMode.FullCSharp),
            new CorpusIntentRow("empty-override.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Definition override layering over a @model(){{dynamic}} root: an override arrives materialized from the ParseContext the parser already built, and emits as an ordinary definition call. Renders model-less on both tiers and is still pinned by HeddleTemplateTests.",
                bom: true),
            new CorpusIntentRow("def-layering.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The layering shapes in one model-less document: a full override calling the layer below it, props declared on two layers, a three-deep chain where each layer reaches only the one under it, an override declared inside another definition's body, and caller content spliced separately at each layer."),
            new CorpusIntentRow("expr-flagship.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Native-expression flagship: HED1004 requires a typed model, so it cannot compile model-less; NativeExpressionGoldenTests.FlagshipModel supplies Price, Quantity, Name, IsFeatured, Total, Amount, Count and A, and NativeExpressionGoldenTests owns its bytes."),
            new CorpusIntentRow("expr-functions.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Native-expression function fixture: typed-model-only (HED1004); NativeExpressionGoldenTests.FunctionsModel supplies Name, Padded, Negative, A, B and Ratio."),
            new CorpusIntentRow("partial.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "An HTML row fragment whose one hard call is @money(Cost){{@(Locale)}} — a bodied step-back encoder of the ctx-encoding-bodied family — over a dynamic root, so it is an ordinary emission. Model-less, hence its own discriminator on both tiers.",
                bom: true),
            new CorpusIntentRow("props-card.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Props card fixture: its native-expression reads (HED1004) need the typed root PropRoot supplies; PropsGoldenTests owns its bytes."),
            new CorpusIntentRow("raw.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "@raw over a host-provided value with embedded C# the engine refuses under Native (position 27); the build fails the same way, so the entry resolves but never renders.",
                bom: true),
            new CorpusIntentRow("recursion.heddle", CorpusTier.Compiles, CorpusRender.WithModel,
                "Embedded-C# recursion flagship with a dynamic root: the engine refuses its C# under Native (positions 139, 774), and compiles it under FullCSharp — EmbeddedCSharpFragmentTests pins the byte-identical precompile, which is why this row carries the mode.",
                bom: true, mode: ExpressionMode.FullCSharp),
            new CorpusIntentRow("regr-import-multiline-override.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The cross-file override page of the hidden-token regression. Layering an IMPORTED definition is layering like any other — the import is expanded into this document before the parse that builds the layers — so it compiles with the rest of them; MultilineOverrideOffsetRegressionTests still pins the runtime bytes."),
            new CorpusIntentRow("template.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "The original flagship document: its embedded C# is refused under Native (six sites from position 491), and no FullCSharp precompile of its escape-bearing @using body is pinned anywhere, so the row's mode stays Native and the build fails. HeddleTemplateTests owns its bytes.",
                bom: true),
            new CorpusIntentRow("tuple_array.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "The tuple document: the bodied unnamed carrier emits, but the tuple C# is refused under Native (positions 81, 205), so the build fails. HeddleTemplateTests owns its bytes."),
            new CorpusIntentRow("vc-test.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "The double-render (W08) subject document: five of its widgets are full overrides, which emit as ordinary definition calls, and it renders model-less on both tiers."),
            new CorpusIntentRow("wierd-whitespace.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "Whitespace-torture document whose @root reads are embedded C#, which the engine itself will not compile outside FullCSharp — the dynamic tier reports two refusals for this document (positions 655, 1051) under the row's own options, and the build fails the same way. HeddleTemplateTests owns its bytes.",
                bom: true),

            // Late-bound function fixtures: every one of these names a function no registry binds, which is now
            // emitted as a late-bound value site (resolved once at first render through the engine's own ranker)
            // instead of costing the file its tier. With none registered BOTH tiers refuse the same way at render,
            // which is what earns the ResolveOnly rows and is asserted rather than assumed.
            new CorpusIntentRow("fn-late-bound.heddle", CorpusTier.Compiles, CorpusRender.ResolveOnly,
                "Calls a function resolvable from neither the default table nor any referenced export; LateBoundFunctionTests owns its bytes and its ranking."),
            new CorpusIntentRow("fn-standalone-late-bound.heddle", CorpusTier.Compiles, CorpusRender.ResolveOnly,
                "A standalone late-bound call: deferral needs no consumer to hang off, and the record carries no refusal."),
            new CorpusIntentRow("fn-typed-consumer-late-bound.heddle", CorpusTier.Compiles, CorpusRender.ResolveOnly,
                "A late-bound call under a typed consumer: the site still defers through the engine's ranker instead of refusing."),
            new CorpusIntentRow("fn-unresolvable-marker.heddle", CorpusTier.Compiles, CorpusRender.ResolveOnly,
                "Was the corpus's marker entry until late binding arrived. Its outer call's ARGUMENT is an expression over a second unbindable name — but an argument is neither a bodied nor a chained consumer, so no class-(c) refusal fires and the site late-binds like any other value site. UnresolvableFunctionTests owns its classification and its diagnostic."),

            // These entries assert the diagnostic's IDENTITY, not offsets into a hand-counted string, which is why
            // they are corpus entries at all; position probes stay inline in their own tests.

            new CorpusIntentRow("ergo-import-broken.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "Imports a target that does not resolve; the front end errors (HED0003) and the build fails it the same way."),
            new CorpusIntentRow("import-origin-a.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture: the error must be attributed to this file, not to the library — and the build failing it is that attribution."),
            new CorpusIntentRow("import-origin-b.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture, second hop of the chain; a front-end error on both tiers."),
            new CorpusIntentRow("import-origin-broken.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture: the deliberately broken origin; a front-end error on both tiers."),
            new CorpusIntentRow("import-origin-c.heddle", CorpusTier.EngineError, CorpusRender.ResolveOnly,
                "Import-origin attribution fixture, third hop of the chain; a front-end error on both tiers."),

            // Chains. The corpus carried none at all while the build refused them, so these are the shared home
            // for the shape rather than an adaptation of one.
            new CorpusIntentRow("chain-output.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
                "Multi-item output chains — two definitions around a definition producer, and a definition around a registered function — model-less, so both tiers render them and the bytes are compared."),
            new CorpusIntentRow("chain-parameter.heddle", CorpusTier.Compiles, CorpusRender.Standalone,
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

        /// <summary>Every declared file name that produces an artifact, in either compiling tier,
        /// ordinal-sorted. The right-hand side of the gates that ask about the build alone, which cannot see
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
