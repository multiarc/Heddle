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

        /// <summary>Route through an inference helper so an awkward type is never spelled. Declared for the
        /// selector a later stage adds; no selector chooses it yet.</summary>
        Generic,

        /// <summary>Resolve once at first use — delegate-only functions, computed <c>@partial</c> names. Declared
        /// for the selector a later stage adds; no selector chooses it yet.</summary>
        LateBound,

        /// <summary>Compute the value the way the engine computes it, once, into a static accessor delegate.
        /// Declared for the selector a later stage adds; no selector chooses it yet.</summary>
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

        public static EmissionPlan Refused(Refusal refusal) => new EmissionPlan(EmissionPlanKind.Refuse, refusal);
    }
}
