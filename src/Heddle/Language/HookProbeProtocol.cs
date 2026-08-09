using System;
using System.Collections.Generic;

namespace Heddle.Language
{
    /// <summary>Which of the three probe documents an observation came from.</summary>
    internal enum HookProbeDocument
    {
        /// <summary>The call value is a plain sentinel (<see cref="HookProbeProtocol.SData"/>).</summary>
        A,

        /// <summary>The call value is a sequence of sentinels
        /// (<c>IEnumerable&lt;<see cref="HookProbeProtocol.SElement"/>&gt;</c>). The pair A/B is what separates a
        /// hook that passes its data through from one that takes the sequence apart.</summary>
        B,

        /// <summary>A bodiless call, for the hook's return value alone: <c>null</c> is the zero-output protocol.</summary>
        C
    }

    /// <summary>Which sentinel a probed type turned out to be. The probe's alphabet: every answer a hook can give is
    /// one of the four sentinels, the sequence of one of them, the loop index, "no static type", or something the
    /// protocol does not recognise.</summary>
    internal enum HookProbeSentinel
    {
        /// <summary>A type the protocol has no name for — the answer that must never appear, and the reason
        /// <see cref="HookProbeOutcome.Unclassified"/> exists.</summary>
        Unrecognized = 0,

        /// <summary>No type at all: the hook returned <c>null</c>, or nothing was observed.</summary>
        Absent,

        /// <summary><see cref="HookProbeProtocol.SParent"/> — the enclosing scope's model.</summary>
        Parent,

        /// <summary><see cref="HookProbeProtocol.SData"/> — the call's own positional value, in document A.</summary>
        Data,

        /// <summary><c>IEnumerable&lt;<see cref="HookProbeProtocol.SElement"/>&gt;</c> — the call's own positional
        /// value, in document B. The same role as <see cref="Data"/>, seen through the other document.</summary>
        DataSequence,

        /// <summary><see cref="HookProbeProtocol.SChained"/> — the value on the chained channel, put there by the
        /// preceding <c>@param</c>.</summary>
        Chained,

        /// <summary><see cref="HookProbeProtocol.SElement"/> — document B's element type, reached only by a hook that
        /// opens the sequence up.</summary>
        Element,

        /// <summary><see cref="int"/> — the iteration index.</summary>
        Int32,

        /// <summary>No static type (<c>dynamic</c>). Document A's answer from a hook that wanted a sequence and was
        /// handed a scalar.</summary>
        Dynamic
    }

    /// <summary>What the probe could conclude about one extension.</summary>
    internal enum HookProbeOutcome
    {
        /// <summary>The A/B pair agreed on a role for both channels: <see cref="HookProbeResult.Body"/> and
        /// <see cref="HookProbeResult.Chained"/> are answers.</summary>
        Classified,

        /// <summary>The hook compiles no body at all (<c>@param</c>, the <c>@import</c> tombstone), so there is no
        /// body typing to name. Not a failure — a different shape.</summary>
        NoBody,

        /// <summary>The two documents disagreed, or an answer was a type the protocol has no role for. The caller
        /// must treat the extension as unprobeable and fall back; it must never guess.</summary>
        Unclassified
    }

    /// <summary>One document's answers, already reduced to sentinel roles by the per-tier driver. Deliberately holds
    /// no <c>Type</c>, no <c>ExType</c> and no <c>ISymbol</c>: the drivers differ in what they read the engine's
    /// state with, and this is the vocabulary they agree in.</summary>
    internal readonly struct HookProbeObservation
    {
        public HookProbeObservation(bool bodyCompiled, HookProbeSentinel bodyModel, HookProbeSentinel bodyChained,
            HookProbeSentinel returned)
        {
            BodyCompiled = bodyCompiled;
            BodyModel = bodyModel;
            BodyChained = bodyChained;
            Returned = returned;
        }

        /// <summary>Whether the hook drove a child body compile at all.</summary>
        public bool BodyCompiled { get; }

        /// <summary>The model type the body was compiled against — the hook's <c>dataType</c> argument to
        /// <c>InitSubTemplate</c>, verbatim.</summary>
        public HookProbeSentinel BodyModel { get; }

        /// <summary>The chained type the body was compiled against — the hook's <c>chainedType</c> argument.</summary>
        public HookProbeSentinel BodyChained { get; }

        /// <summary>The hook's return value, and <see cref="HookProbeSentinel.Absent"/> for <c>null</c>.
        /// <para>Meaningful for document <see cref="HookProbeDocument.C"/> only. A and B are chained documents
        /// carrying two calls, and the protocol deliberately does not disambiguate their two return values — the one
        /// question the return answers (zero output) needs a single unchained call to answer it cleanly.</para></summary>
        public HookProbeSentinel Returned { get; }
    }

    /// <summary>What the probe decided about one extension: a role per channel, never a type.</summary>
    internal readonly struct HookProbeResult
    {
        public HookProbeResult(HookProbeOutcome outcome, BodyModelSource body, ChainedModelSource chained,
            bool zeroOutput)
        {
            Outcome = outcome;
            Body = body;
            Chained = chained;
            ZeroOutput = zeroOutput;
        }

        public HookProbeOutcome Outcome { get; }

        /// <summary>Meaningful only when <see cref="Outcome"/> is <see cref="HookProbeOutcome.Classified"/>.</summary>
        public BodyModelSource Body { get; }

        /// <summary>Meaningful only when <see cref="Outcome"/> is <see cref="HookProbeOutcome.Classified"/>.</summary>
        public ChainedModelSource Chained { get; }

        /// <summary>The hook returned <c>null</c> from a bodiless call: it produces no output at all. Independent of
        /// <see cref="Outcome"/> — the <c>@import</c> tombstone is <see cref="HookProbeOutcome.NoBody"/> and
        /// zero-output at once.</summary>
        public bool ZeroOutput { get; }
    }

    /// <summary>
    /// The hook probe's <b>protocol</b>: the sentinel types a hook is asked about, the three documents it is asked
    /// through, and the pure function that turns what came back into a role. Data and decode only — the drivers that
    /// actually run a compile live per tier (a direct one on the engine side, a reflection one over a loaded engine
    /// on the build side), because compile-time state is reachable by different means from each.
    /// <para><b>Why a probe at all.</b> The generator's largest coverage loss is extension hooks: it cannot read what
    /// an <c>InitStart</c> override decides, so it hardcodes a name-keyed table for the handful it knows and refuses
    /// the rest. The table is a prediction. Running the hook is the answer — and it works without the consumer's model
    /// types, because what a hook does with its three type arguments is a property of the extension, not of the
    /// caller's types. <i>Harvest what is a function of the extension; compute symbolically what is a function of the
    /// consumer's types.</i></para>
    /// <para><b>The output is a role, never a type.</b> The emitter keeps computing the body's actual type from the
    /// call site. The probe replaces the table's <b>rows</b>, not its <b>vocabulary</b>: it answers in
    /// <see cref="BodyModelSource"/> and <see cref="ChainedModelSource"/>.</para>
    /// <para><b>Shared-source constraints.</b> netstandard2.0, no Roslyn, no IO, no diagnostics, no engine types —
    /// the file is linked into both tiers by the <c>Language\**</c> glob, so anything it names must exist in both.</para>
    /// </summary>
    internal static class HookProbeProtocol
    {
        /// <summary>The root scope type of every probe document: the hook's <c>parent</c> argument, by construction.
        /// Its three members are the three values a probe document can hand a call.</summary>
        internal sealed class SParent
        {
            /// <summary>The plain call value of document <see cref="HookProbeDocument.A"/>.</summary>
            public SData Data { get; set; }

            /// <summary>The value handed to the trailing <c>@param</c>, which returns its own data type and so puts
            /// this on the chained channel of the call under probe.</summary>
            public SChained Chained { get; set; }

            /// <summary>The call value of document <see cref="HookProbeDocument.B"/>. Generic on purpose: a hook that
            /// derives an element type reaches <see cref="SElement"/> through it, and one that does not passes the
            /// sequence itself through.</summary>
            public IEnumerable<SElement> Items { get; set; }
        }

        /// <summary>The call's positional value.</summary>
        internal sealed class SData
        {
        }

        /// <summary>The value on the chained channel.</summary>
        internal sealed class SChained
        {
        }

        /// <summary>The element type document <see cref="HookProbeDocument.B"/>'s sequence is built over.</summary>
        internal sealed class SElement
        {
        }

        /// <summary>The one extension name the protocol hardcodes, and the only one it may: <c>@param</c> returns its
        /// own data type without compiling anything, which is what lets a probe document put a chosen type on the
        /// chained channel of the call under test.
        /// <para>It is therefore also the <b>bootstrap</b>. Probe it first: if <c>@param</c> does not behave as
        /// described (<see cref="IsBootstrapSound"/>), the loaded engine is not the one this protocol was written for,
        /// and every other answer it gives is unsafe to believe. Refuse all probing rather than degrade one
        /// extension.</para></summary>
        internal const string BootstrapExtensionName = "param";

        /// <summary>The body text of documents A and B. Its length is the only thing that identifies the recorded
        /// body-compile span, so it must not be a substring of the surrounding document.</summary>
        internal const string BodyMarker = "MARKMARK";

        /// <summary>The <see cref="SParent"/> member each document reads. Spelled once, here, so the document text and
        /// the sentinel declarations cannot drift apart.</summary>
        private const string DataMember = nameof(SParent.Data);

        private const string SequenceMember = nameof(SParent.Items);

        private const string ChainedMember = nameof(SParent.Chained);

        /// <summary>The probe document for <paramref name="extensionName"/>, compiled against
        /// <see cref="SParent"/> as the root scope type.
        /// <list type="bullet">
        /// <item><description>A — <c>@name(Data):param(Chained){{MARKMARK}}</c></description></item>
        /// <item><description>B — <c>@name(Items):param(Chained){{MARKMARK}}</c></description></item>
        /// <item><description>C — <c>@name(Data)</c></description></item>
        /// </list>
        /// <para>Compile errors are expected and are not the answer: a hook whose <c>[DataType]</c> the sentinel does
        /// not satisfy still runs, because the engine collects that error and initializes the extension anyway. The
        /// driver reads state, not success.</para>
        /// </summary>
        internal static string Document(string extensionName, HookProbeDocument document)
        {
            if (extensionName == null)
                throw new ArgumentNullException(nameof(extensionName));

            switch (document)
            {
                case HookProbeDocument.A:
                    return "@" + extensionName + "(" + DataMember + "):" + BootstrapExtensionName + "(" +
                           ChainedMember + "){{" + BodyMarker + "}}";
                case HookProbeDocument.B:
                    return "@" + extensionName + "(" + SequenceMember + "):" + BootstrapExtensionName + "(" +
                           ChainedMember + "){{" + BodyMarker + "}}";
                case HookProbeDocument.C:
                    return "@" + extensionName + "(" + DataMember + ")";
                default:
                    throw new ArgumentOutOfRangeException(nameof(document));
            }
        }

        /// <summary>The sentinel a probed type is, given the type and whether the engine called it <c>dynamic</c>.
        /// The one place a <c>Type</c> appears: both drivers hold real <see cref="Type"/> objects here (the build-side
        /// one reads them back off a loaded engine's <c>ExType</c> by reflection), so this stays shared rather than
        /// becoming a seam.</summary>
        internal static HookProbeSentinel Classify(Type type, bool isDynamic)
        {
            // Dynamic first: the engine spells it as object-with-a-flag, so the flag is the discriminator.
            if (isDynamic)
                return HookProbeSentinel.Dynamic;
            if (type == null)
                return HookProbeSentinel.Absent;
            if (type == typeof(SParent))
                return HookProbeSentinel.Parent;
            if (type == typeof(SData))
                return HookProbeSentinel.Data;
            if (type == typeof(SChained))
                return HookProbeSentinel.Chained;
            if (type == typeof(SElement))
                return HookProbeSentinel.Element;
            if (type == typeof(IEnumerable<SElement>))
                return HookProbeSentinel.DataSequence;
            if (type == typeof(int))
                return HookProbeSentinel.Int32;
            return HookProbeSentinel.Unrecognized;
        }

        /// <summary>The bootstrap check, run on <see cref="BootstrapExtensionName"/>'s own three observations before
        /// any other extension is believed: <c>@param</c> compiles no body and returns its own data type. Both halves
        /// matter — the first says the driver correctly sees "no body compiled" rather than mis-reading a span, the
        /// second is the property the chained channel of documents A and B is built on.</summary>
        internal static bool IsBootstrapSound(in HookProbeObservation a, in HookProbeObservation b,
            in HookProbeObservation c) =>
            !a.BodyCompiled && !b.BodyCompiled && c.Returned == HookProbeSentinel.Data;

        /// <summary>Turns the three observations into a role per channel. Pure: the same observations always decode to
        /// the same result, on either tier.
        /// <para>The A/B <b>pair</b> is what makes <see cref="BodyModelSource.ElementOfData"/> distinguishable from
        /// <see cref="BodyModelSource.Data"/> at all — a pass-through hook answers A with the scalar and B with the
        /// sequence, while one that opens the sequence up answers A with <c>dynamic</c> (there is no element type in a
        /// scalar) and B with the element. Neither document alone separates them.</para>
        /// <para>Anything else is <see cref="HookProbeOutcome.Unclassified"/>, deliberately and without a guess: an
        /// unprobeable extension costs its call site the precompiled tier, which is the safe direction, and a wrong
        /// role costs correctness.</para>
        /// </summary>
        internal static HookProbeResult Decode(in HookProbeObservation a, in HookProbeObservation b,
            in HookProbeObservation c)
        {
            bool zeroOutput = c.Returned == HookProbeSentinel.Absent;

            if (!a.BodyCompiled || !b.BodyCompiled)
            {
                // Both documents must agree that there is no body. One of each is the two documents disagreeing about
                // the hook's shape, which is exactly what Unclassified is for.
                return a.BodyCompiled == b.BodyCompiled
                    ? new HookProbeResult(HookProbeOutcome.NoBody, default, default, zeroOutput)
                    : Unclassified(zeroOutput);
            }

            if (!TryBody(a.BodyModel, b.BodyModel, out var body))
                return Unclassified(zeroOutput);
            if (!TryChained(a.BodyChained, b.BodyChained, out var chained))
                return Unclassified(zeroOutput);

            return new HookProbeResult(HookProbeOutcome.Classified, body, chained, zeroOutput);
        }

        private static HookProbeResult Unclassified(bool zeroOutput) =>
            new HookProbeResult(HookProbeOutcome.Unclassified, default, default, zeroOutput);

        private static bool TryBody(HookProbeSentinel a, HookProbeSentinel b, out BodyModelSource body)
        {
            if (a == HookProbeSentinel.Parent && b == HookProbeSentinel.Parent)
            {
                body = BodyModelSource.Parent;
                return true;
            }

            if (a == HookProbeSentinel.Data && b == HookProbeSentinel.DataSequence)
            {
                body = BodyModelSource.Data;
                return true;
            }

            if (a == HookProbeSentinel.Chained && b == HookProbeSentinel.Chained)
            {
                body = BodyModelSource.Chained;
                return true;
            }

            if (a == HookProbeSentinel.Dynamic && b == HookProbeSentinel.Element)
            {
                body = BodyModelSource.ElementOfData;
                return true;
            }

            body = default;
            return false;
        }

        private static bool TryChained(HookProbeSentinel a, HookProbeSentinel b, out ChainedModelSource chained)
        {
            if (a == HookProbeSentinel.Chained && b == HookProbeSentinel.Chained)
            {
                chained = ChainedModelSource.None;
                return true;
            }

            if (a == HookProbeSentinel.Int32 && b == HookProbeSentinel.Int32)
            {
                chained = ChainedModelSource.Int32Index;
                return true;
            }

            if (a == HookProbeSentinel.Parent && b == HookProbeSentinel.Parent)
            {
                chained = ChainedModelSource.Parent;
                return true;
            }

            if (a == HookProbeSentinel.Data && b == HookProbeSentinel.DataSequence)
            {
                chained = ChainedModelSource.Data;
                return true;
            }

            chained = default;
            return false;
        }
    }
}
