using Heddle.Strings.Core;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// The machine-readable classification of a precompilation refusal. Three members name the only legitimate
    /// end-state refusal kinds — a genuine CLR/csc wall, a value the build cannot know, and an engine failure the
    /// degrade reproduces by letting the dynamic tier raise it — and the rest are operational groupings of what the
    /// emitter cannot yet emit, each the retire-target of a planned capability. Members are stable: tests pin them
    /// through the degrade-expectation seam, so a member whose population has emptied is <b>retired in place</b>
    /// (<see cref="ClrWall"/>, <see cref="DefinitionLayering"/>) and never deleted, and coverage regressions stay
    /// measurable per category.
    /// <para>Every member here classifies a decline that costs the <b>template</b> its tier. A decline that costs
    /// one call site instead has no category at all: it is not a refusal, it is an emission
    /// (<c>PrecompiledRuntime.SiteFallback</c>), and it reports as <c>HED7033</c> or as nothing.</para>
    /// </summary>
    internal enum RefusalCategory
    {
        /// <summary>No C# produces the engine's bytes — a genuine CLR/csc wall. Declared and reached by nothing:
        /// every wall found so far turned out to be a <i>sink</i> question (<see cref="RefLikeSink"/>) or a
        /// <i>spelling</i> question (<see cref="UnnameableType"/>), both of which are narrower and recover more.
        /// Retired in place rather than deleted — the degrade-expectation seam pins these members by name.</summary>
        ClrWall,

        /// <summary>A value or type that decides the bytes exists only at run time (or is chosen by reflection
        /// order), and the build cannot know or thread it.</summary>
        UnknowableValue,

        /// <summary>The engine refuses or fails here and the generator would render — the degrade reproduces the
        /// failure by handing the template to the tier whose diagnostic is the contract.</summary>
        EngineParity,

        /// <summary>A chained call, bodied carrier or chain item the emitter cannot flatten.</summary>
        ChainCarrier,

        /// <summary>Definition override/layering. Reached by nothing today: the emitter refused every
        /// <c>&lt;name:name&gt;</c> on the belief that it resolved definitions flatly and so could only ever reach
        /// the most-derived layer, and it never resolved them itself — it asks the same <c>ParseContext</c> the
        /// engine asks, and the parser has already put the layer each call site sees in it. Declared and unreached,
        /// as <see cref="ClrWall"/> is: the degrade-expectation seam pins these members by name, so a member is
        /// retired in place rather than deleted.</summary>
        DefinitionLayering,

        /// <summary>A definition or extension prop prototype the emitter cannot freeze — unknown, duplicate,
        /// missing or unreproducible values, and dynamic arguments it cannot type.</summary>
        DefinitionProps,

        /// <summary>An embedded C# expression the compiled-fragment path cannot carry — no in-file <c>@model</c> to
        /// pin the scope, a read of <c>root</c> where nothing pins the root type, a <c>@using</c> body naming no
        /// namespace, text the engine's own compiler rejects, or a position inside a type-agnostic body, where the
        /// fragment's model parameter would have to be spelled before the hook has answered.</summary>
        EmbeddedCSharp,

        /// <summary>A function call no build-time registration binds, and no late-bound site can serve — calls the
        /// shared ranker refuses.</summary>
        FunctionBinding,

        /// <summary>The <b>substitute's own bound</b>. A hook the build has not read is no longer a decline at all
        /// — the extension's real <c>InitStart</c> runs at static-init and chooses its body's typing — so what is
        /// left here is the one thing the per-call-site substitute cannot be honest about: it compiles the call's own
        /// text as its own document, which sees no enclosing definition, no ambient region fill scope and no active
        /// prop layout. A body reaching for any of those costs the <b>template</b> rather than the call, and this
        /// names that, not the construct inside the body that sent the emitter to the substitute in the first
        /// place. It also names a call shape a type-agnostic body cannot carry, whose value would have to be typed
        /// before the hook has answered.</summary>
        HookBehavior,

        /// <summary>An extension the binder cannot bind at build time, or a binding-surface fault (unbindable
        /// type, unbound name, malformed <c>[Prop]</c> declaration).</summary>
        ExtensionBinding,

        /// <summary>The consumer's build configuration forbids the emission — an expression-mode gate, a missing
        /// reference, or conflicting model declarations. The remedy is host setup, not template or emitter work.</summary>
        HostSetup,

        /// <summary>A member path, prop read or receiver the emitter cannot resolve statically.</summary>
        MemberAccess,

        /// <summary>A native expression shape the shared operator tables or the expression writer do not emit, or a
        /// computed one inside a type-agnostic body: its result type depends on an operand type that does not exist
        /// until the hook answers, and the DLR's numeric promotion is not the engine's for every operand pair, so
        /// routing it there would be wrong rather than slow.</summary>
        NativeExpression,

        /// <summary>An <c>@partial</c> name the emitter cannot evaluate or normalize.</summary>
        PartialName,

        /// <summary>A ref-struct value in a boxing sink — recoverable only in the rendered sink, where the
        /// carrier's own protocol stringifies in place.</summary>
        RefLikeSink,

        /// <summary>An <c>@out</c>/slot shape the emitter cannot carry.</summary>
        SlotChannel,

        /// <summary>A type generated code cannot spell — inaccessible, error-obsolete, unresolvable, or with no
        /// writable name.</summary>
        UnnameableType
    }

    /// <summary>
    /// One categorized refusal: the <see cref="Category"/> is the machine-readable classification the
    /// degrade-expectation seam pins, the <see cref="Detail"/> is the human-readable sentence HED7031 prints
    /// (unchanged from the free-form reasons it replaces), and the <see cref="Position"/> is the refusing
    /// construct's template span where the site has one. This is the emitter's only way to record a refusal —
    /// a bare string reason cannot reach the degrade channel.
    /// </summary>
    internal sealed class Refusal
    {
        public Refusal(RefusalCategory category, string detail, BlockPosition position = default)
        {
            Category = category;
            Detail = detail;
            Position = position;
        }

        public RefusalCategory Category { get; }
        public string Detail { get; }
        public BlockPosition Position { get; }
    }
}
