using System.Collections.Generic;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>The projection-equivalence corpus: one template per diagnostic block plus a clean control, each with
    /// the <c>(Id, IsWarning, Offset, Length)</c> multiset the shared <c>HeddleDiagnosticProjection</c> drain
    /// produces — and the per-host deltas each host <b>declares</b> rather than merely exhibits.</para>
    /// <para>Three suites assert against this one table, so the hosts are compared to each other transitively and
    /// a host that silently drops a channel turns the suite red: <c>Heddle.Tests</c> (the drain itself, the
    /// parse-channel subset, and <c>HeddleCompileResult</c>), <c>Heddle.LanguageServices.Tests</c> (the editor),
    /// and <c>Heddle.Generator.Tests</c> (the build tier). The file is linked into the latter two, following a
    /// shared precedent.</para>
    /// <para>Every value here was <b>measured</b>, not derived. Templates deliberately avoid model members: a
    /// typeless editor session types the model as <c>null</c> and a runtime compile types it as
    /// <c>ExType.Dynamic</c>, so a member path would draw <c>HED0001</c> on one side only — a model-typing
    /// difference, not a drain difference, and not what this corpus is for.</para>
    /// <para><b>Not covered:</b> the <c>HED5xxx</c> declaration block (its templates need an extension assembly
    /// each host registers differently), and cases like import origin re-anchoring and the generator's region-fill
    /// retract filter — which are asserted in their own suites (<c>ImportOriginTests</c>,
    /// <c>DiagnosticProjectionTests.TheIncludePredicateFiltersBeforeProjection</c>) because neither has a cross-host
    /// counterpart to compare against.</para>
    /// </summary>
    internal static class DiagnosticCorpusVectors
    {
        /// <summary>One fixture. Entries are spelled <c>"HED0003/E@23,0"</c> — id, <c>E</c>/<c>W</c>, offset,
        /// length — so a mismatch prints as one readable string rather than a struct dump.</summary>
        internal sealed class Case
        {
            public Case(string name, string template, string[] entries, string[] textProfileEntries,
                string[] parseChannel, string[] buildTwins, string[] buildForwarded = null)
            {
                Name = name;
                Template = template;
                Entries = entries;
                TextProfileEntries = textProfileEntries ?? entries;
                ParseChannel = parseChannel;
                BuildTwins = buildTwins;
                BuildForwarded = buildForwarded ?? parseChannel;
            }

            /// <summary>Fixture name, used in assertion messages.</summary>
            public string Name { get; }

            /// <summary>The template text. Identical bytes on all three hosts.</summary>
            public string Template { get; }

            /// <summary>What the full drain reports under the <c>Html</c> profile — the default the engine, the
            /// build tier and the editor all share.</summary>
            public string[] Entries { get; }

            /// <summary>The same under <c>Text</c>. Differs only for the encoding lint, which is what makes the
            /// profile default a user-visible choice rather than a detail.</summary>
            public string[] TextProfileEntries { get; }

            /// <summary>The subset reachable from the parse channel alone. It stopped being the same thing as
            /// what the build tier forwards once the generator gained the shaping-time compile warnings, so the
            /// two are separate columns and the difference between them is legible per fixture.</summary>
            public string[] ParseChannel { get; }

            /// <summary>What the <b>build</b> tier forwards under the front end's own ids. It is the parse channel
            /// plus whichever compile-channel warnings the generator's own walk reaches; a fixture where the two
            /// columns differ is one the build tier used to report nothing for.</summary>
            public string[] BuildForwarded { get; }

            /// <summary>Ids the build tier raises <i>itself</i> for this fixture instead of forwarding the front
            /// end's — the <c>HED7xxx</c> twins. Not a drain delta; a deliberate build-tier diagnostic.</summary>
            public string[] BuildTwins { get; }

            public override string ToString() => Name;
        }

        private static readonly string[] None = new string[0];

        internal static IReadOnlyList<Case> Cases { get; } = new[]
        {
            new Case("syntax", "@list(x){{ unterminated",
                new[] { "HED0003/E@23,0" }, null,
                new[] { "HED0003/E@23,0" }, None),

            // The name is unknown to BOTH tiers, but only the run tier can say so: the build knows the call's
            // shape and emits a late-bound site for it, so it raises no twin of its own here. The HED7014 twin
            // moved to the fixture below, which is the shape late binding cannot serve.
            new Case("unknownFunction", "@(nosuchfunc(1))",
                new[] { "HED1001/E@2,13" }, null,
                None, None),

            // Two unknown names, the inner one an argument of the outer: the inner call's return type is the
            // value no build-time answer exists for, so the outer call has nothing to rank against and the build
            // raises its own HED7014 instead of emitting a late-bound site. The run tier stops at the innermost
            // unknown name, which is why one entry and not two.
            new Case("unresolvableFunctionArgument", "@(nosuchfunc(alsonone(1) + 1))",
                new[] { "HED1001/E@2,27" }, null,
                None, new[] { "HED7014" }),

            new Case("unknownProfile", "@profile(){{xml}}\nhi\n",
                new[] { "HED2001/E@1,9" }, null,
                None, new[] { "HED7022" }),

            new Case("encodingLint", "<a href=\"@(1)\">t</a>",
                new[] { "HED2004/W@10,3" }, None,
                None, None, new[] { "HED2004/W@10,3" }),

            new Case("orphanElif", "@elif(true){{1}}",
                new[] { "HED3002/W@1,10" }, null,
                None, None, new[] { "HED3002/W@1,10" }),

            new Case("orphanElse", "@else(){{1}}",
                new[] { "HED3003/E@1,6" }, null,
                None, None),

            new Case("rangeStep", "@for(range(1, 5, 0)){{x}}",
                new[] { "HED4001/E@17,1" }, null,
                None, None),

            new Case("legacyImport", "@import(){{gone}}\nhello\n",
                new[] { "HED4003/E@1,8" }, null,
                new[] { "HED4003/E@1,8" }, None),

            new Case("braceMisread", "hello {{ Title }} world",
                new[] { "HED4005/W@6,2" }, null,
                None, None, new[] { "HED4005/W@6,2" }),

            new Case("strippedGap", "@if(true){{a}}GAP@else(){{b}}",
                new[] { "HED3001/W@18,6" }, null,
                None, None, new[] { "HED3001/W@18,6" }),

            new Case("elseCondition", "@if(true){{a}}@else(true){{b}}",
                new[] { "HED3004/W@15,10" }, null,
                None, None, new[] { "HED3004/W@15,10" }),

            new Case("profileAfterOutput", "@(1)\n@profile(){{text}}\nx",
                new[] { "HED2002/W@6,9" }, null,
                None, None, new[] { "HED2002/W@6,9" }),

            new Case("doubleRender", "@%\n<card> -> ()\n{{CARD}}\n%@\n@card()",
                new[] { "HED4002/W@29,6" }, null,
                None, None, new[] { "HED4002/W@29,6" }),

            new Case("clean", "hello world", None, None, None, None),
        };
    }
}
