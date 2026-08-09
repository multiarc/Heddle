using Heddle.Strings.Core;

namespace Heddle.Generator.Emit
{
    /// <summary>
    /// The machine-readable classification of a precompilation refusal. Three members name the only legitimate
    /// end-state refusal kinds — a genuine CLR/csc wall, a value the build cannot know, and an engine failure the
    /// degrade reproduces by letting the dynamic tier raise it — and the rest are operational groupings of what the
    /// emitter cannot yet emit, each the retire-target of a planned capability. Members are stable: tests pin them
    /// through the degrade-expectation seam, so coverage regressions are measurable per category.
    /// </summary>
    internal enum RefusalCategory
    {
        /// <summary>No C# produces the engine's bytes — a genuine CLR/csc wall.</summary>
        ClrWall,

        /// <summary>A value or type that decides the bytes exists only at run time (or is chosen by reflection
        /// order), and the build cannot know or thread it.</summary>
        UnknowableValue,

        /// <summary>The engine refuses or fails here and the generator would render — the degrade reproduces the
        /// failure by handing the template to the tier whose diagnostic is the contract.</summary>
        EngineParity,

        /// <summary>A chained call, bodied carrier or chain item the emitter cannot flatten.</summary>
        ChainCarrier,

        /// <summary>Definition override/layering, a control-flow divergence outside the value-escape boundary.</summary>
        DefinitionLayering,

        /// <summary>A definition or extension prop prototype the emitter cannot freeze — unknown, duplicate,
        /// missing or unreproducible values, and dynamic arguments it cannot type.</summary>
        DefinitionProps,

        /// <summary>An embedded C# expression the compiled-fragment path cannot carry.</summary>
        EmbeddedCSharp,

        /// <summary>A function call no build-time registration binds, and no late-bound site can serve — calls the
        /// shared ranker refuses.</summary>
        FunctionBinding,

        /// <summary>Extension behavior only a compile-time hook knows — <c>InitStart</c>/<c>CompleteInit</c>
        /// overrides, bodied custom extensions, and missing body model-typing rows.</summary>
        HookBehavior,

        /// <summary>An extension the binder cannot bind at build time, or a binding-surface fault (unbindable
        /// type, unbound name, malformed <c>[Prop]</c> declaration).</summary>
        ExtensionBinding,

        /// <summary>The consumer's build configuration forbids the emission — an expression-mode gate, a missing
        /// reference, or conflicting model declarations. The remedy is host setup, not template or emitter work.</summary>
        HostSetup,

        /// <summary>A member path, prop read or receiver the emitter cannot resolve statically.</summary>
        MemberAccess,

        /// <summary>A native expression shape the shared operator tables or the expression writer do not emit.</summary>
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
