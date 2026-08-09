using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <b>The gate on the hook-probing thesis.</b> The claim is that running an extension's compile-time hook answers
    /// what <see cref="BodyModelRules"/>' hand-written table predicts — and answers it for every extension, not just
    /// the seven the table pins. This suite is where that is proved or disproved, before a line of loader code exists.
    /// Any disagreement between the probe and the table is a stop: either the table is wrong (fix the table) or the
    /// protocol is wrong (a design fault), and there is no third reading.
    /// <para>The probe is ground truth here, not the table. Adjusting the probe until it agreed with the table would
    /// invert the whole point — the table is the artifact under suspicion, and it was already caught being wrong once
    /// (the <c>@list</c> row's chained column).</para>
    /// </summary>
    public class HookProbeLockstepTests
    {
        /// <summary>One declared answer: what the probe must say about one registered extension.</summary>
        private readonly struct ExpectedRow
        {
            public ExpectedRow(string name, HookProbeOutcome outcome, BodyModelSource body,
                ChainedModelSource chained, bool zeroOutput)
            {
                Name = name;
                Outcome = outcome;
                Body = body;
                Chained = chained;
                ZeroOutput = zeroOutput;
            }

            public string Name { get; }
            public HookProbeOutcome Outcome { get; }
            public BodyModelSource Body { get; }
            public ChainedModelSource Chained { get; }
            public bool ZeroOutput { get; }

            public override string ToString() =>
                Outcome == HookProbeOutcome.Classified
                    ? $"({Outcome}, {Body}, {Chained}, zeroOutput: {ZeroOutput})"
                    : $"({Outcome}, zeroOutput: {ZeroOutput})";
        }

        /// <summary>Everything the probe should say about every extension the engine registers, enumerated rather than
        /// sampled — the unit of a finding here is a whole extension, so a name missing from this table is a gap, not
        /// a saving. <see cref="EveryRegisteredExtensionIsInThisTable"/> asserts both directions.</summary>
        private static ExpectedRow[] Expected() => new[]
        {
            // --- Parent-bodied: the body executes in the caller's scope, and the chained value passes through. ---
            Row("if", BodyModelSource.Parent, ChainedModelSource.None),
            Row("ifnot", BodyModelSource.Parent, ChainedModelSource.None),
            Row("elif", BodyModelSource.Parent, ChainedModelSource.None),
            Row("elseif", BodyModelSource.Parent, ChainedModelSource.None),
            Row("else", BodyModelSource.Parent, ChainedModelSource.None),
            // The step-back encoders: @string's default-body shape, shared verbatim by eight more built-ins. The
            // generator's hardcoded StepBackEncoders list names four of them and is silently missing the rest.
            Row("string", BodyModelSource.Parent, ChainedModelSource.None),
            Row("attr", BodyModelSource.Parent, ChainedModelSource.None),
            Row("url", BodyModelSource.Parent, ChainedModelSource.None),
            Row("js", BodyModelSource.Parent, ChainedModelSource.None),
            Row("int", BodyModelSource.Parent, ChainedModelSource.None),
            Row("money", BodyModelSource.Parent, ChainedModelSource.None),
            Row("date", BodyModelSource.Parent, ChainedModelSource.None),
            Row("time", BodyModelSource.Parent, ChainedModelSource.None),
            Row("guid", BodyModelSource.Parent, ChainedModelSource.None),

            // --- The two index hosts. ---
            Row("for", BodyModelSource.Parent, ChainedModelSource.Int32Index),
            Row("list", BodyModelSource.ElementOfData, ChainedModelSource.Int32Index),

            // --- Data-bodied: the base InitStart, unoverridden or passed straight through. ---
            Row("", BodyModelSource.Data, ChainedModelSource.None),
            Row("raw", BodyModelSource.Data, ChainedModelSource.None),
            Row("html", BodyModelSource.Data, ChainedModelSource.None),
            Row("partial", BodyModelSource.Data, ChainedModelSource.None),
            Row("model", BodyModelSource.Data, ChainedModelSource.None, zeroOutput: true),
            Row("using", BodyModelSource.Data, ChainedModelSource.None, zeroOutput: true),
            Row("profile", BodyModelSource.Data, ChainedModelSource.None, zeroOutput: true),

            // --- The channel swappers. Neither role had a name in the table's vocabulary before the probe. ---
            Row("out", BodyModelSource.Chained, ChainedModelSource.Parent),
            Row("swap", BodyModelSource.Chained, ChainedModelSource.Data),

            // --- No body compile at all. @param returns its value untouched; @import is an inert tombstone. ---
            NoBody("param"),
            NoBody("import", zeroOutput: true)
        };

        private static ExpectedRow Row(string name, BodyModelSource body, ChainedModelSource chained,
            bool zeroOutput = false) =>
            new ExpectedRow(name, HookProbeOutcome.Classified, body, chained, zeroOutput);

        private static ExpectedRow NoBody(string name, bool zeroOutput = false) =>
            new ExpectedRow(name, HookProbeOutcome.NoBody, default, default, zeroOutput);

        /// <summary>The bootstrap, asserted first and separately: if <c>@param</c> does not compile no body and return
        /// its own data type, the engine under test is not the one the protocol was written for and no other row in
        /// this file means anything.</summary>
        [Fact]
        public void TheBootstrapHolds()
        {
            Assert.True(HookProbeDriver.BootstrapHolds(),
                "@param no longer compiles no body and returns its data type. The probe documents put a chosen type " +
                "on the chained channel through exactly that property, so every other answer is unsafe: refuse " +
                "probing rather than trusting a single result.");
        }

        /// <summary><b>The go/no-go.</b> For every name <see cref="BodyModelRules"/> pins, the probe must produce the
        /// same row. This is the whole thesis in one assertion: the table is a prediction, the probe is the
        /// observation, and they must not differ.</summary>
        [Fact]
        public void TheProbeAgreesWithEveryPinnedTableRow()
        {
            var disagreements = new List<string>();
            foreach (var name in BodyModelRules.PinnedNames.OrderBy(n => n, StringComparer.Ordinal))
            {
                Assert.True(BodyModelRules.TryGet(name, out var body, out var chained));
                var probed = HookProbeDriver.Probe(name);
                if (probed.Outcome != HookProbeOutcome.Classified || probed.Body != body ||
                    probed.Chained != chained)
                {
                    disagreements.Add($"<{name}>: table says ({body}, {chained}), probe says " +
                                      $"({probed.Outcome}, {probed.Body}, {probed.Chained})");
                }
            }

            Assert.True(disagreements.Count == 0,
                "The hook probe and the pinned table disagree. Decide per row whether the TABLE is wrong (fix the " +
                "row and extend BodyModelRuleTableTests) or the PROTOCOL is wrong (a design fault — stop). Never " +
                "adjust the probe to agree with the table.\n" + string.Join("\n", disagreements));
        }

        /// <summary>The probe's answer for every extension the engine ships, table-pinned or not — the payoff, since
        /// 20 of the 27 names have no table row at all and today cost their call sites the precompiled tier for
        /// it.</summary>
        [Fact]
        public void TheProbeAnswersEveryBuiltInExtension()
        {
            var mismatches = new List<string>();
            foreach (var expected in Expected())
            {
                var probed = HookProbeDriver.Probe(expected.Name);
                bool agrees = probed.Outcome == expected.Outcome && probed.ZeroOutput == expected.ZeroOutput &&
                              (probed.Outcome != HookProbeOutcome.Classified ||
                               (probed.Body == expected.Body && probed.Chained == expected.Chained));
                if (!agrees)
                {
                    mismatches.Add($"<{expected.Name}>: declared {expected}, probed (" +
                                   $"{probed.Outcome}, {probed.Body}, {probed.Chained}, " +
                                   $"zeroOutput: {probed.ZeroOutput})");
                }
            }

            Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
        }

        /// <summary>Every extension the engine ships, discovered by the engine's <b>own</b> rule rather than a
        /// reimplementation of it — so a new built-in cannot be added without an answer here, and a name whose
        /// discovery rule changes is caught rather than silently dropped.
        /// <para>Deliberately not the live registry: <c>TemplateFactory</c> is process-global and other suites in this
        /// assembly register their own fixture extensions into it, so a set-equality gate against it would pass or
        /// fail on test order. The shipped set is the stable question, and every member of it is asserted registered
        /// anyway.</para></summary>
        [Fact]
        public void EveryBuiltInExtensionIsInThisTable()
        {
            var declared = Expected().Select(row => row.Name).ToList();
            Assert.Equal(declared.Count, declared.Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(
                BuiltInNames().ToArray(),
                declared.OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.All(declared, name => Assert.True(TemplateFactory.Exists(name), "<" + name + "> is not registered."));
        }

        /// <summary>The names the engine assembly ships, in ordinal order.</summary>
        private static IEnumerable<string> BuiltInNames() =>
            BuiltIns().Select(e => e.Name).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal);

        private static IEnumerable<ExtensionType> BuiltIns() =>
            TemplateFactory.LoadExtensions(typeof(HeddleTemplate).Assembly);

        /// <summary>No built-in decodes to <see cref="HookProbeOutcome.Unclassified"/>. The outcome exists for the
        /// third-party hook the protocol's alphabet cannot describe; a built-in reaching it would mean the vocabulary
        /// is short a role.</summary>
        [Fact]
        public void NoBuiltInExtensionIsUnclassified()
        {
            var unclassified = BuiltInNames()
                .Where(n => HookProbeDriver.Probe(n).Outcome == HookProbeOutcome.Unclassified)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();
            Assert.True(unclassified.Count == 0,
                "Built-in extensions the probe cannot classify: " + string.Join(", ", unclassified));
        }

        /// <summary>The double-probe rule, which the build tier will apply to third-party extensions to catch
        /// nondeterminism and cross-instance state: two fresh runs must agree, or the extension is refused. Asserted
        /// here for the built-ins so the rule has a working reference.</summary>
        [Fact]
        public void ProbingTwiceGivesTheSameAnswer()
        {
            foreach (var name in BuiltInNames())
            {
                var first = HookProbeDriver.Probe(name);
                var second = HookProbeDriver.Probe(name);
                Assert.Equal(first.Outcome, second.Outcome);
                Assert.Equal(first.Body, second.Body);
                Assert.Equal(first.Chained, second.Chained);
                Assert.Equal(first.ZeroOutput, second.ZeroOutput);
            }
        }

        /// <summary>The zero-output protocol is the <c>null</c> return, observed — not a name list and not the
        /// <c>[ZeroOutput]</c> attribute. The two agree today, and this asserts the agreement rather than picking a
        /// side: the attribute is the declaration, the null return is the behaviour.</summary>
        [Fact]
        public void TheNullReturnIsTheZeroOutputProtocol()
        {
            foreach (var extension in BuiltIns())
            {
                bool declared = extension.Type
                    .GetCustomAttributes(typeof(Heddle.Attributes.ZeroOutputAttribute), true).Length != 0;
                Assert.Equal(declared, HookProbeDriver.Probe(extension.Name).ZeroOutput);
            }
        }

        // ---- The decode function itself, over synthetic observations: the arms no built-in reaches. ----

        /// <summary>A hook whose two documents disagree is <see cref="HookProbeOutcome.Unclassified"/>, never a guess
        /// at the more likely of the two. This is the arm every third-party extension the protocol cannot describe
        /// lands on, and no built-in exercises it.</summary>
        [Fact]
        public void DisagreeingOrUnknownBodyAnswersAreUnclassified()
        {
            var pairs = new[]
            {
                (HookProbeSentinel.Parent, HookProbeSentinel.Data),
                (HookProbeSentinel.Data, HookProbeSentinel.Element),
                (HookProbeSentinel.Unrecognized, HookProbeSentinel.Unrecognized)
            };

            foreach (var (a, b) in pairs)
            {
                var result = HookProbeProtocol.Decode(
                    new HookProbeObservation(true, a, HookProbeSentinel.Chained, HookProbeSentinel.Unrecognized),
                    new HookProbeObservation(true, b, HookProbeSentinel.Chained, HookProbeSentinel.Unrecognized),
                    new HookProbeObservation(false, HookProbeSentinel.Absent, HookProbeSentinel.Absent,
                        HookProbeSentinel.Data));
                Assert.Equal(HookProbeOutcome.Unclassified, result.Outcome);
            }
        }

        /// <summary>A readable body role with an unreadable chained one is still Unclassified: a half-answer is not an
        /// answer, because the emitter needs both channels to type the body.</summary>
        [Fact]
        public void AnUnreadableChainedAnswerUnclassifiesTheWholeRow()
        {
            var result = HookProbeProtocol.Decode(
                new HookProbeObservation(true, HookProbeSentinel.Parent, HookProbeSentinel.Element,
                    HookProbeSentinel.Unrecognized),
                new HookProbeObservation(true, HookProbeSentinel.Parent, HookProbeSentinel.Element,
                    HookProbeSentinel.Unrecognized),
                new HookProbeObservation(false, HookProbeSentinel.Absent, HookProbeSentinel.Absent,
                    HookProbeSentinel.Data));
            Assert.Equal(HookProbeOutcome.Unclassified, result.Outcome);
        }

        /// <summary>One document compiling a body and the other not is a disagreement about the hook's very shape, so
        /// it is Unclassified rather than <see cref="HookProbeOutcome.NoBody"/>.</summary>
        [Fact]
        public void ABodyInOneDocumentOnlyIsUnclassified()
        {
            var result = HookProbeProtocol.Decode(
                new HookProbeObservation(true, HookProbeSentinel.Parent, HookProbeSentinel.Chained,
                    HookProbeSentinel.Unrecognized),
                new HookProbeObservation(false, HookProbeSentinel.Absent, HookProbeSentinel.Absent,
                    HookProbeSentinel.Unrecognized),
                new HookProbeObservation(false, HookProbeSentinel.Absent, HookProbeSentinel.Absent,
                    HookProbeSentinel.Data));
            Assert.Equal(HookProbeOutcome.Unclassified, result.Outcome);
        }

        /// <summary>The sentinel alphabet is closed: a type outside it is <see cref="HookProbeSentinel.Unrecognized"/>
        /// rather than quietly the nearest role, and <c>dynamic</c> beats the <c>object</c> the engine spells it
        /// with.</summary>
        [Fact]
        public void ClassifyNamesOnlyTheProtocolsOwnTypes()
        {
            Assert.Equal(HookProbeSentinel.Parent,
                HookProbeProtocol.Classify(typeof(HookProbeProtocol.SParent), false));
            Assert.Equal(HookProbeSentinel.DataSequence,
                HookProbeProtocol.Classify(typeof(IEnumerable<HookProbeProtocol.SElement>), false));
            Assert.Equal(HookProbeSentinel.Int32, HookProbeProtocol.Classify(typeof(int), false));
            Assert.Equal(HookProbeSentinel.Absent, HookProbeProtocol.Classify(null, false));
            Assert.Equal(HookProbeSentinel.Dynamic, HookProbeProtocol.Classify(typeof(object), true));
            Assert.Equal(HookProbeSentinel.Unrecognized, HookProbeProtocol.Classify(typeof(object), false));
            Assert.Equal(HookProbeSentinel.Unrecognized, HookProbeProtocol.Classify(typeof(string), false));
        }
    }
}
