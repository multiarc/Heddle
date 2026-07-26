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
    /// and <c>Heddle.Generator.Tests</c> (the build tier). The file is linked into the latter two, following the
    /// <c>LineIndexVectors</c> precedent.</para>
    /// <para>Every value here was <b>measured</b>, not derived. Templates deliberately avoid model members: a
    /// typeless editor session types the model as <c>null</c> and a runtime compile types it as
    /// <c>ExType.Dynamic</c>, so a member path would draw <c>HED0001</c> on one side only — a model-typing
    /// difference, not a drain difference, and not what this corpus is for.</para>
    /// <para><b>Not covered:</b> the <c>HED5xxx</c> declaration block (its templates need an extension assembly
    /// each host registers differently), and the two host-policy cases D12.5 names — import origin re-anchoring
    /// and the generator's region-fill retract filter — which are asserted in their own suites
    /// (<c>ImportOriginTests</c>, <c>DiagnosticProjectionTests.TheIncludePredicateFiltersBeforeProjection</c>)
    /// because neither has a cross-host counterpart to compare against.</para>
    /// </summary>
    internal static class DiagnosticCorpusVectors
    {
        /// <summary>One fixture. Entries are spelled <c>"HED0003/E@23,0"</c> — id, <c>E</c>/<c>W</c>, offset,
        /// length — so a mismatch prints as one readable string rather than a struct dump.</summary>
        internal sealed class Case
        {
            public Case(string name, string template, string[] entries, string[] textProfileEntries,
                string[] parseChannel, string[] buildTwins)
            {
                Name = name;
                Template = template;
                Entries = entries;
                TextProfileEntries = textProfileEntries ?? entries;
                ParseChannel = parseChannel;
                BuildTwins = buildTwins;
            }

            /// <summary>Fixture name, used in assertion messages.</summary>
            public string Name { get; }

            /// <summary>The template text. Identical bytes on all three hosts.</summary>
            public string Template { get; }

            /// <summary>What the full drain reports under the <c>Html</c> profile — the default the engine, the
            /// build tier and (since the phase 6 second pass) the editor all share.</summary>
            public string[] Entries { get; }

            /// <summary>The same under <c>Text</c>. Differs only for the encoding lint, which is what makes the
            /// profile default a user-visible choice rather than a detail.</summary>
            public string[] TextProfileEntries { get; }

            /// <summary>The subset reachable from the parse channel alone — i.e. exactly what the generator can
            /// forward today, because it runs no compile-channel stage. Declaring it per fixture turns the
            /// program's recorded compile-channel gap from prose into a measured, gated fact.</summary>
            public string[] ParseChannel { get; }

            /// <summary>Ids the build tier raises <i>itself</i> for this fixture instead of forwarding the front
            /// end's — the <c>HED7xxx</c> twins. Not a drain delta; a deliberate build-tier diagnostic.</summary>
            public string[] BuildTwins { get; }

            public override string ToString() => Name;
        }

        private static readonly string[] None = new string[0];

        internal static IReadOnlyList<Case> Cases { get; } = new[]
        {
            // HED0xxx — a parse-channel syntax error; one of only two entries the build tier can forward today.
            new Case("syntax", "@list(x){{ unterminated",
                new[] { "HED0003/E@23,0" }, null,
                new[] { "HED0003/E@23,0" }, None),

            // HED1xxx — unknown function. Compile channel; the build tier raises HED7014 instead.
            new Case("unknownFunction", "@(nosuchfunc(1))",
                new[] { "HED1001/E@2,13" }, null,
                None, new[] { "HED7014" }),

            // HED2xxx — an unknown @profile value. Compile channel; the build tier raises HED7022.
            new Case("unknownProfile", "@profile(){{xml}}\nhi\n",
                new[] { "HED2001/E@1,9" }, null,
                None, new[] { "HED7022" }),

            // HED2004 — the encoding lint, and the only fixture whose verdict depends on the profile. Under Text
            // it is silent; under Html it fires. It is here so "the editor lints like the build of record" is a test.
            new Case("encodingLint", "<a href=\"@(1)\">t</a>",
                new[] { "HED2004/W@10,3" }, None,
                None, None),

            // HED3xxx warning — an orphan @elif compiles, warns, and behaves as @if.
            new Case("orphanElif", "@elif(true){{1}}",
                new[] { "HED3002/W@1,10" }, null,
                None, None),

            // HED3xxx error — an orphan @else does not.
            new Case("orphanElse", "@else(){{1}}",
                new[] { "HED3003/E@1,6" }, null,
                None, None),

            // HED4xxx — a non-positive range step, positioned at the argument.
            new Case("rangeStep", "@for(range(1, 5, 0)){{x}}",
                new[] { "HED4001/E@17,1" }, null,
                None, None),

            // HED4003 — the legacy @import directive. The second of the two parse-channel entries.
            new Case("legacyImport", "@import(){{gone}}\nhello\n",
                new[] { "HED4003/E@1,8" }, null,
                new[] { "HED4003/E@1,8" }, None),

            // The control: a template that must produce nothing anywhere. A host that starts inventing
            // diagnostics fails here first.
            new Case("clean", "hello world", None, None, None, None),
        };
    }
}
