namespace Heddle.Generator.Binding
{
    /// <summary>Why a function-call binder refused a call.</summary>
    internal enum BindRefusalKind
    {
        Bound,

        /// <summary>The generator refused without proving anything: an argument the operand estimator could not
        /// type, a cast target it has no C# spelling for, or a binding shape it does not emit. A silent degrade to
        /// the dynamic tier is the correct — and the only sound — reaction.</summary>
        Unproven,

        /// <summary>The <b>shared</b> ranker proved the call illegal: the flat Pareto front had more than one member
        /// (<c>BindOutcome.Ambiguous</c>) or no candidate was applicable (<c>BindOutcome.None</c>), over arguments
        /// every one of which the estimator typed. The runtime, running the same core over the same facts, reaches
        /// the same verdict and raises a compile error — so the build must raise <c>HED7025</c> rather than stay
        /// quiet about an illegality it has already computed.</summary>
        ProvenIllegal
    }

    /// <summary>
    /// A binder's verdict about *why* it returned no binding — distinguishing cases that degrade silently from
    /// cases where the build must report an error.
    /// <para>Both binders used to collapse every refusal to a bare <c>null</c>, which conflated "provably
    /// ambiguous" with "an argument I could not type". Only the first is a proof about the runtime; reporting the
    /// second would fail the build for templates that are perfectly legal, and reporting neither is the silence the
    /// match principle and the fallback-legitimacy principle both forbid.</para>
    /// </summary>
    internal readonly struct BindRefusal
    {
        private BindRefusal(BindRefusalKind kind, string detail, string runtimeDiagnosticId)
        {
            Kind = kind;
            Detail = detail;
            RuntimeDiagnosticId = runtimeDiagnosticId;
        }

        public BindRefusalKind Kind { get; }

        /// <summary>For <see cref="BindRefusalKind.ProvenIllegal"/>: the runtime-shaped sentence naming the call and
        /// its candidate signatures, so the author reading <c>HED7025</c> sees which overloads collided. Null
        /// otherwise.</summary>
        public string Detail { get; }

        /// <summary>For <see cref="BindRefusalKind.ProvenIllegal"/>: the run-tier diagnostic id this build error is
        /// the twin of (<c>HED1013</c> ambiguous, <c>HED1012</c> no applicable overload). Null otherwise.</summary>
        public string RuntimeDiagnosticId { get; }

        public static readonly BindRefusal Bound = new BindRefusal(BindRefusalKind.Bound, null, null);

        public static readonly BindRefusal Unproven = new BindRefusal(BindRefusalKind.Unproven, null, null);

        public static BindRefusal ProvenIllegal(string detail, string runtimeDiagnosticId) =>
            new BindRefusal(BindRefusalKind.ProvenIllegal, detail, runtimeDiagnosticId);
    }
}
