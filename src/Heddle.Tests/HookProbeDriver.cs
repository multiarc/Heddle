using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;

namespace Heddle.Tests
{
    /// <summary>
    /// The <b>engine-side</b> driver for <see cref="HookProbeProtocol"/>: it runs the protocol's documents through the
    /// real compiler, in-process, against real extension instances. The build tier's driver will do the same thing by
    /// reflection over a separately loaded engine; this one exists so the protocol can be proved against the engine
    /// that defines the answers, before any loader code exists.
    /// <para><b>It does not call <c>InitStart</c>.</b> <c>InitContext.SourceItem</c> is an internal field
    /// <c>OutExtension.InitStart</c> reads to decide whether the call carries a value, so a hand-built context makes
    /// <c>@out</c> behave as if it were valueless, and reproducing <c>InitializeTemplate</c>'s preamble is the
    /// simulate-the-engine mistake the whole idea rejects. It asks the engine to compile a one-call template through
    /// public API instead, and reads two pieces of state the engine already keeps:</para>
    /// <list type="bullet">
    /// <item><description><c>ScopeMap</c> — recorded at the top of <i>every</i> body compile when
    /// <c>ProvideLanguageFeatures</c> is set. <c>AbstractExtension.InitSubTemplate</c> drives the hook's child compile
    /// through that same funnel, so the body entry's model and chained types <b>are</b> the two arguments the hook
    /// chose, verbatim.</description></item>
    /// <item><description><c>CompileContext.CompiledItems[*].ReturnTypeChainedPrevious</c> — the hook's own return
    /// value, and <c>null</c> for the zero-output protocol.</description></item>
    /// </list>
    /// <para>Compile <b>errors are ignored on purpose</b>. A sentinel that does not satisfy a hook's
    /// <c>[DataType]</c> draws HED0004, and <c>@out</c> outside a definition draws HED5012 — both are collected, and
    /// the engine initializes the extension either way. The probe reads decisions, not verdicts.</para>
    /// </summary>
    internal static class HookProbeDriver
    {
        /// <summary>Probes one extension by name. Never throws for a template-level fault: an extension the protocol
        /// cannot read comes back <see cref="HookProbeOutcome.Unclassified"/>.</summary>
        internal static HookProbeResult Probe(string name) =>
            HookProbeProtocol.Decode(
                Observe(name, HookProbeDocument.A),
                Observe(name, HookProbeDocument.B),
                Observe(name, HookProbeDocument.C));

        /// <summary>The bootstrap: <c>@param</c> behaves as the protocol says, so the chained channel of documents A
        /// and B really carries <c>SChained</c> and "no body compiled" really means what it says. False means refuse
        /// every probe, not degrade one.</summary>
        internal static bool BootstrapHolds() =>
            HookProbeProtocol.IsBootstrapSound(
                Observe(HookProbeProtocol.BootstrapExtensionName, HookProbeDocument.A),
                Observe(HookProbeProtocol.BootstrapExtensionName, HookProbeDocument.B),
                Observe(HookProbeProtocol.BootstrapExtensionName, HookProbeDocument.C));

        private static HookProbeObservation Observe(string name, HookProbeDocument document)
        {
            var text = HookProbeProtocol.Document(name, document);
            var context = new CompileContext(
                new TemplateOptions { ProvideLanguageFeatures = true },
                typeof(HookProbeProtocol.SParent));

            // A fresh template per document, so nothing carries over between observations.
            new HeddleTemplate().Compile(text, context);

            var entries = context.ScopeMap?.Entries ?? (IReadOnlyList<ScopeMapEntry>) new ScopeMapEntry[0];

            // Entry 0 is the document itself; the hook's body is the first span recorded inside it. A span whose
            // length is not the marker's is some other compile the hook triggered, not its body — unreadable rather
            // than guessable, so it decodes to Unclassified through the Unrecognized sentinel.
            var body = entries.Skip(1).FirstOrDefault();
            bool compiled = entries.Count > 1;
            bool readable = compiled && body.Length == HookProbeProtocol.BodyMarker.Length;

            var returned = HookProbeSentinel.Unrecognized;
            if (document == HookProbeDocument.C && context.CompiledItems.Count == 1)
                returned = Sentinel(context.CompiledItems.Values.Single().ReturnTypeChainedPrevious);

            return new HookProbeObservation(
                compiled,
                compiled ? (readable ? Sentinel(body.ModelType) : HookProbeSentinel.Unrecognized) : HookProbeSentinel.Absent,
                compiled ? (readable ? Sentinel(body.ChainedType) : HookProbeSentinel.Unrecognized) : HookProbeSentinel.Absent,
                returned);
        }

        private static HookProbeSentinel Sentinel(ExType type) =>
            HookProbeProtocol.Classify(type?.Type, type != null && type.IsDynamic);
    }
}
