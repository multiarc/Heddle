namespace Heddle.Generator.Emit
{
    /// <summary>
    /// How a value node's bytes are produced — chosen per node from its resolved type and its sink, never inside
    /// a writer. Selection is ordered: <see cref="Refuse"/> is the LAST exit, reachable only after every other
    /// plan has been considered, which is what keeps a single unsupported mechanism from over-refusing a value
    /// some cheaper plan already produces byte-identically.
    /// </summary>
    internal enum EmissionPlanKind
    {
        /// <summary>Typed C# in place — today's emission.</summary>
        Direct,

        /// <summary><c>.ToString()</c> in place of the box — legal for the rendered sink, whose carrier protocol
        /// (<c>value is string s ? s : value.ToString()</c>) never needs the boxed value.</summary>
        Stringified,

        /// <summary>Route through an inference helper — or a generic entry point — so an awkward type is never
        /// spelled. Still without a selector, and the stage that went looking for one found out why: the types this
        /// plan would hide are the types the dynamic tier cannot serve either. An open generic model is refused by
        /// the engine's own render-time model gate for every value a caller could pass, and by its expression-tree
        /// model accessor before that, so hiding it behind a type parameter would render where the engine refuses;
        /// a type this compilation merely may not name is already served by <see cref="EngineAccessor"/>, which
        /// computes it the engine's way instead of spelling it. Kept as the ladder's declared rung so the next
        /// candidate joins it rather than growing a fourth mechanism.</summary>
        Generic,

        /// <summary>Resolve once at first use, then cache. Chosen for a function call whose target only a
        /// run-time registration supplies: the call's SHAPE is known at build time and is exactly what the
        /// engine's overload ranker takes, so a generated <c>PrecompiledFunctionSite</c> replays that selection
        /// against the live registry at first render. Selected in
        /// <see cref="NativeExpressionWriter"/>'s call writer, which is where the targets are — a member path has
        /// none, so <see cref="TemplateEmitter"/>'s value-plan ladder never reaches this.
        /// <para>The other deferred resolution named for this plan, the computed <c>@partial</c> name, landed
        /// earlier through <c>PrecompiledPartialName</c>: the engine evaluates that one at ITS compile time, so it
        /// resolves at static init rather than at first use, and it never joins this ladder.</para></summary>
        LateBound,

        /// <summary>Compute the value the way the engine computes it: a static accessor delegate built once at
        /// type-init from the engine's own member resolution, called once per render. Chosen for a member-path
        /// value the engine resolves but generated C# cannot spell — never for control flow, which stays fully
        /// precompiled or degrades the template.</summary>
        EngineAccessor,

        /// <summary>No plan produces the engine's bytes; carries a categorized <see cref="Refusal"/>.</summary>
        Refuse
    }

    /// <summary>A selected plan. Writers execute it and decide nothing — a refused plan carries the categorized
    /// reason with it, so the decision and its explanation travel as one value.</summary>
    internal readonly struct EmissionPlan
    {
        private EmissionPlan(EmissionPlanKind kind, Refusal refusal)
        {
            Kind = kind;
            Refusal = refusal;
        }

        public EmissionPlanKind Kind { get; }

        /// <summary>Non-null exactly when <see cref="Kind"/> is <see cref="EmissionPlanKind.Refuse"/>.</summary>
        public Refusal Refusal { get; }

        public static EmissionPlan Direct => new EmissionPlan(EmissionPlanKind.Direct, null);

        public static EmissionPlan Stringified => new EmissionPlan(EmissionPlanKind.Stringified, null);

        public static EmissionPlan EngineAccessor => new EmissionPlan(EmissionPlanKind.EngineAccessor, null);

        public static EmissionPlan Refused(Refusal refusal) => new EmissionPlan(EmissionPlanKind.Refuse, refusal);
    }
}
