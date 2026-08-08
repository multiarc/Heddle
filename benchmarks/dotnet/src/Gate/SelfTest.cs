using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Heddle.Benchmarks.Dotnet.Corpus;

namespace Heddle.Benchmarks.Dotnet.Gate
{
    /// <summary>
    /// Harness self-checks. These test the gate itself, not the engines: a gate with a bug in its
    /// normalization either passes drifted output or fails correct output, and both are worse than
    /// no gate. Every other ecosystem ships the equivalent (benchmarks/js/test/gate-selftest.mjs).
    ///
    /// The normalization here is a sixth independent implementation of contract v2, so it is
    /// checked two ways: against the committed corpus (which is already in stored form, so the
    /// pipeline must be the identity on it) and against fixed vectors whose expected values are
    /// pinned from the JS reference implementation.
    /// </summary>
    public static class SelfTest
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static int Run()
        {
            var failures = new List<string>();
            var checks = 0;

            checks += TechniqueDifferential(failures);
            checks += SinkMaterialisation(failures);
            checks += ComposedPageModelMatchesGolden(failures);
            checks += NormalizationIsIdentityOnStoredForm(failures);
            checks += StripWhitespaceMatchesReference(failures);
            checks += EntityCanonicalizationVectors(failures);
            checks += WhitespaceClassIsClosed(failures);
            checks += InterTagCollapseVectors(failures);
            checks += WellFormednessRejectsLoneSurrogate(failures);

            foreach (var f in failures) Console.Error.WriteLine("[FAIL] " + f);
            Console.WriteLine($"\nselftest: {checks - failures.Count} passed, {failures.Count} failed (of {checks} checks).");
            return failures.Count == 0 ? 0 : 1;
        }

        /// <summary>
        /// The six-technique differential: every available Heddle render technique must produce
        /// byte-identical output on every workload it covers.
        ///
        /// This is what stops a technique from "winning" by doing less. A sink that skipped
        /// encoding, or a backend that silently fell through to the dynamic path, would show up
        /// here as a divergence rather than as a suspiciously good number in the technique table.
        /// </summary>
        private static int TechniqueDifferential(List<string> failures)
        {
            var checks = 0;
            var sinks = new[] { Engines.HeddleEngine.Sink.String,
                                Engines.HeddleEngine.Sink.TextWriter,
                                Engines.HeddleEngine.Sink.Utf8 };

            foreach (var (workload, _) in GoldenCorpus.Workloads)
            {
                var reference = Engines.HeddleEngine.Render("controlled", workload, Engines.HeddleEngine.Sink.String);
                var precompiledCovered = Engines.Precompiled.Entries()
                    .ContainsKey((workload == "composed-page" ? "home" : workload) + ".heddle");

                foreach (var sink in sinks)
                {
                    checks++;
                    var got = Engines.HeddleEngine.Render("controlled", workload, sink);
                    if (!string.Equals(got, reference, StringComparison.Ordinal))
                        failures.Add($"technique differential: runtime/{Engines.HeddleEngine.SinkLabel(sink)} " +
                                     $"diverges from runtime/string on {workload} at index {FirstDiff(got, reference)}");

                    if (!precompiledCovered) continue;
                    checks++;
                    var pre = Engines.Precompiled.Render("controlled", workload, sink);
                    if (!string.Equals(pre, reference, StringComparison.Ordinal))
                        failures.Add($"technique differential: precompiled/{Engines.HeddleEngine.SinkLabel(sink)} " +
                                     $"diverges from runtime/string on {workload} at index {FirstDiff(pre, reference)}");
                }
            }
            return checks;
        }

        /// <summary>
        /// The de-optimization guard, asserted rather than assumed: each streaming sink must have
        /// actually handled every unit of output.
        ///
        /// The UTF-8 sink's byte count is checked against the golden's own UTF-8 length, and the
        /// TextWriter's character count against the rendered string. A sink that elided work would
        /// come up short here; a sink that merely counted without reading would still be caught by
        /// the checksum, which is computed from the bytes the engine wrote.
        /// </summary>
        private static int SinkMaterialisation(List<string> failures)
        {
            var checks = 0;
            foreach (var (workload, _) in GoldenCorpus.Workloads)
            {
                // Populates the Last* telemetry as a side effect of rendering through each sink.
                var asString = Engines.HeddleEngine.Render("controlled", workload, Engines.HeddleEngine.Sink.String);
                Engines.HeddleEngine.Render("controlled", workload, Engines.HeddleEngine.Sink.TextWriter);
                var viaUtf8 = Engines.HeddleEngine.Render("controlled", workload, Engines.HeddleEngine.Sink.Utf8);

                checks++;
                if (Engines.HeddleEngine.LastTextWriterCount != asString.Length)
                    failures.Add($"materialisation: {workload} TextWriter saw " +
                                 $"{Engines.HeddleEngine.LastTextWriterCount} chars, render produced {asString.Length}");

                checks++;
                if (Engines.HeddleEngine.LastTextWriterHash != HashChars(asString))
                    failures.Add($"materialisation: {workload} TextWriter checksum does not match the rendered string");

                checks++;
                var expectedBytes = Utf8NoBom.GetByteCount(asString);
                if (Engines.HeddleEngine.LastUtf8ByteCount != expectedBytes)
                    failures.Add($"materialisation: {workload} UTF-8 sink wrote " +
                                 $"{Engines.HeddleEngine.LastUtf8ByteCount} bytes, render is {expectedBytes} bytes");

                checks++;
                if (!string.Equals(viaUtf8, asString, StringComparison.Ordinal))
                    failures.Add($"materialisation: {workload} UTF-8 sink bytes decode to different text");
            }
            return checks;
        }

        private static ulong HashChars(string s) => Materialisation.HashOf(s);

        /// <summary>
        /// The MATERIALISATION-CHECK trailer the gate prints, mirroring the JS harness's. Runs the
        /// same assertions as <see cref="SinkMaterialisation"/> and reduces them to one verdict
        /// line, so a run that stopped producing output fails the sweep rather than annotating a
        /// table nobody re-reads.
        /// </summary>
        public static (bool Clean, string Line) MaterialisationTrailer()
        {
            var failures = new List<string>();
            SinkMaterialisation(failures);
            TechniqueDifferential(failures);
            if (failures.Count == 0)
                return (true, "MATERIALISATION-CHECK: clean");
            return (false, "MATERIALISATION-CHECK: flagged\n  " + string.Join("\n  ", failures));
        }

        /// <summary>
        /// Model-fidelity check for the composed-page fixtures. Under the E20 full-page shape the
        /// golden is no longer a bare fragment concatenation — chrome literals surround everything —
        /// so fidelity is proven in three independent pieces, all without a template in the loop:
        ///
        ///   (1) each non-empty blob area (<see cref="Models.AreaData"/>) occurs exactly once in
        ///       the golden, in <see cref="Models.TwinContent.AreaOrder"/> order;
        ///   (2) an independent C# expansion of <see cref="Models.NavData"/> into the nav markup —
        ///       a SECOND implementation of the fragment bodies the Heddle templates define —
        ///       occurs verbatim (whitespace-stripped) in the golden, mega menus and footer both;
        ///   (3) every NavLink's Label+Href pair occurs as a rendered link.
        ///
        /// If these pass, a port that carried AreaData and nav.json across intact reproduces the
        /// same navigation bytes the oracle holds.
        /// </summary>
        private static int ComposedPageModelMatchesGolden(List<string> failures)
        {
            var golden = Normalize.StripWhitespace(GoldenCorpus.Load("composed-page").Text);
            var areas = Models.TwinContent.Areas;

            // (1) blob areas: present exactly once, in order.
            var cursor = 0;
            foreach (var name in Models.TwinContent.AreaOrder)
            {
                if (!areas.TryGetValue(name, out var fragment))
                {
                    failures.Add($"composed-page model: AreaOrder names \"{name}\" but AreaData has no such key");
                    continue;
                }
                var stripped = Normalize.StripWhitespace(Models.TwinContent.Normalize(fragment));
                if (stripped.Length == 0) continue; // "Alert Top Section Below Nav" is pinned empty
                var count = Normalize.CountOccurrences(golden, stripped);
                if (count != 1)
                {
                    failures.Add($"composed-page model: blob area \"{name}\" occurs {count} time(s) in the golden (expected 1)");
                    continue;
                }
                var at = golden.IndexOf(stripped, StringComparison.Ordinal);
                if (at < cursor)
                    failures.Add($"composed-page model: blob area \"{name}\" appears out of AreaOrder order");
                else
                    cursor = at + stripped.Length;
            }

            // (2) the independent nav expansion. The two menus render adjacently (one @list over
            // Nav.Menus), as do the four footer columns, so each group must occur as one block.
            var nav = Models.NavData.Model();
            var menus = new StringBuilder();
            foreach (var menu in nav.Menus) AppendMenuExpansion(menus, menu);
            if (!golden.Contains(Normalize.StripWhitespace(menus.ToString()), StringComparison.Ordinal))
                failures.Add("composed-page model: the independent C# expansion of NavData's mega menus " +
                             "does not occur in the golden");

            var footer = new StringBuilder();
            foreach (var column in nav.FooterColumns) AppendColumnExpansion(footer, column);
            if (!golden.Contains(Normalize.StripWhitespace(footer.ToString()), StringComparison.Ordinal))
                failures.Add("composed-page model: the independent C# expansion of NavData's footer columns " +
                             "does not occur in the golden");

            // (3) every link pair, as rendered.
            var missing = 0;
            string firstMissing = null;
            void CheckLinks(Models.NavColumn column)
            {
                foreach (var s in column.Sections)
                    foreach (var l in s.Links)
                    {
                        var needle = Normalize.StripWhitespace(
                            $"<li class=\"nav-link\"><a href=\"{l.Href}\">{l.Label}</a></li>");
                        if (!golden.Contains(needle, StringComparison.Ordinal))
                        {
                            missing++;
                            firstMissing ??= $"{l.Label} -> {l.Href}";
                        }
                    }
            }
            foreach (var menu in nav.Menus)
                foreach (var tab in menu.Tabs)
                    foreach (var column in tab.Columns)
                        CheckLinks(column);
            foreach (var column in nav.FooterColumns) CheckLinks(column);
            if (missing > 0)
                failures.Add($"composed-page model: {missing} NavLink pair(s) missing from the golden " +
                             $"(first: {firstMissing})");

            return 3;
        }

        /// <summary>The second implementation of the mega-menu fragment bodies (deliberately not
        /// shared with the templates — agreement between independent implementations is the check).</summary>
        private static void AppendMenuExpansion(StringBuilder sb, Models.MegaMenu menu)
        {
            sb.Append("<div class=\"top-menu-wrapper\"><ul class=\"top-menu\">");
            foreach (var tab in menu.Tabs)
            {
                sb.Append("<li class=\"").Append(tab.Css).Append("\"><a href=\"").Append(tab.Href)
                  .Append("\" class=\"drop\">").Append(tab.Label).Append("</a>");
                if (tab.HasDropdown)
                {
                    sb.Append("<div class=\"").Append(tab.DropdownCss).Append("\">");
                    foreach (var column in tab.Columns) AppendColumnExpansion(sb, column);
                    sb.Append("</div>");
                }
                sb.Append("</li>");
            }
            sb.Append("</ul></div>");
        }

        private static void AppendColumnExpansion(StringBuilder sb, Models.NavColumn column)
        {
            sb.Append("<div class=\"nav-column\">");
            foreach (var s in column.Sections)
            {
                sb.Append("<div class=\"nav-section\">");
                if (s.TitleLinked)
                    sb.Append("<span class=\"nav-title\"><a href=\"").Append(s.Href).Append("\">")
                      .Append(s.Title).Append("</a></span>");
                else
                    sb.Append("<span class=\"nav-title\">").Append(s.Title).Append("</span>");
                sb.Append("<ul>");
                foreach (var l in s.Links)
                    sb.Append("<li class=\"nav-link\"><a href=\"").Append(l.Href).Append("\">")
                      .Append(l.Label).Append("</a></li>");
                sb.Append("</ul></div>");
            }
            sb.Append("</div>");
        }

        /// <summary>
        /// The corpus is stored post-N1–N5, so re-applying the pipeline must change nothing. This
        /// catches the whole class of "my N3 is too greedy" bugs against 1.09 MB of real output.
        /// </summary>
        private static int NormalizationIsIdentityOnStoredForm(List<string> failures)
        {
            var n = 0;
            foreach (var (id, suite) in GoldenCorpus.Workloads)
            {
                n++;
                var entry = GoldenCorpus.Load(id);
                var again = Normalize.Apply(entry.Text, suite, id);
                if (!string.Equals(again, entry.Text, StringComparison.Ordinal))
                {
                    var at = FirstDiff(again, entry.Text);
                    failures.Add($"normalize is not the identity on stored form for {id}: first diff at {at} " +
                                 $"(stored {entry.Text.Length} chars, re-normalized {again.Length})");
                }
            }
            return n;
        }

        /// <summary>
        /// N3b lengths pinned from the reference implementations. These exact numbers were computed
        /// from the JS harness over the same corpus, so a divergence here means this port disagrees
        /// with the gate the other five ecosystems run.
        /// </summary>
        private static int StripWhitespaceMatchesReference(List<string> failures)
        {
            var expected = new (string Id, int NonWhitespaceBytes)[]
            {
                // composed-page and fragment-heavy were re-pinned at the E20 redesign export from
                // this harness's own N3b over the regenerated corpus; they MUST be re-confirmed
                // against the JS reference implementation when the Stage-3 ports land.
                ("composed-page", 39481),
                ("trivial-substitution", 319),
                ("large-loop", 192780),
                ("mixed-page", 8886),
                ("conditional-heavy", 15248),
                ("fragment-heavy", 5840),
                ("fortunes-encoded", 1055),
                ("encoded-loop", 786685),
            };
            foreach (var (id, want) in expected)
            {
                var stripped = Normalize.StripWhitespace(GoldenCorpus.Load(id).Text);
                var got = Utf8NoBom.GetByteCount(stripped);
                if (got != want)
                    failures.Add($"N3b byte length for {id}: expected {want}, got {got}");
            }
            return expected.Length;
        }

        /// <summary>N5 vectors: every recognized spelling canonicalizes, everything else survives.</summary>
        private static int EntityCanonicalizationVectors(List<string> failures)
        {
            var vectors = new (string Input, string Expected, string Why)[]
            {
                ("&amp;", "&amp;", "named amp is already canonical"),
                ("&apos;", "&#39;", "apos canonicalizes to the numeric form"),
                ("&#39;", "&#39;", "decimal apostrophe is canonical"),
                ("&#039;", "&#39;", "leading zeros are allowed"),
                ("&#x27;", "&#39;", "lowercase hex apostrophe"),
                ("&#X27;", "&#39;", "uppercase X"),
                ("&#x2F;", "&#x2F;", "solidus is not one of the five — untouched"),
                ("&#8482;", "&#8482;", "trade mark is not one of the five — untouched"),
                ("&eacute;", "&eacute;", "unrecognized named entity — untouched"),
                ("&AMP;", "&AMP;", "named entities are case-sensitive"),
                ("&amp;#39;", "&amp;#39;", "replacement is never rescanned (no double-canonicalization)"),
                ("&lt;script&gt;", "&lt;script&gt;", "already canonical"),
                ("&#60;script&#62;", "&lt;script&gt;", "decimal angle brackets"),
                ("a & b", "a & b", "a bare ampersand is not an entity"),
                ("&nosemicolon", "&nosemicolon", "no terminator — not an entity"),
            };
            foreach (var (input, want, why) in vectors)
            {
                var got = Normalize.CanonicalizeEntities(input);
                if (!string.Equals(got, want, StringComparison.Ordinal))
                    failures.Add($"N5 vector ({why}): {Show(input)} -> {Show(got)}, expected {Show(want)}");
            }
            return vectors.Length;
        }

        /// <summary>
        /// The contract's whitespace class is exactly six characters. Unicode whitespace that is
        /// NOT in the class must survive every stage — NBSP in particular is real output a template
        /// can emit, and stripping it would silently mask a genuine divergence.
        /// </summary>
        private static int WhitespaceClassIsClosed(List<string> failures)
        {
            var inClass = new[] { '\t', '\n', '\v', '\f', '\r', ' ' };
            var outOfClass = new[] { '\u00A0', '\u2007', '\u202F', '\u3000', '\u2028', '\u0085' };

            foreach (var c in inClass)
                if (!Normalize.IsContractWhitespace(c))
                    failures.Add($"U+{(int)c:X4} must be in the contract whitespace class");

            foreach (var c in outOfClass)
            {
                if (Normalize.IsContractWhitespace(c))
                    failures.Add($"U+{(int)c:X4} must NOT be in the contract whitespace class");
                var s = "a" + c + "b";
                if (Normalize.StripWhitespace(s) != s)
                    failures.Add($"N3b stripped U+{(int)c:X4}, which is outside the contract class");
                if (Normalize.TrimEdges(c + "x") != c + "x")
                    failures.Add($"N4 trimmed U+{(int)c:X4}, which is outside the contract class");
            }
            return inClass.Length + outOfClass.Length * 3;
        }

        /// <summary>N3 collapses only runs actually bounded by <c>&gt;</c> and <c>&lt;</c>.</summary>
        private static int InterTagCollapseVectors(List<string> failures)
        {
            var vectors = new (string Input, string Expected, string Why)[]
            {
                ("<a>  <b>", "<a><b>", "run between tags collapses"),
                ("<a>\n\t <b>", "<a><b>", "mixed run collapses"),
                ("<a> x <b>", "<a> x <b>", "text between tags is not a whitespace run"),
                ("<a>text</a>", "<a>text</a>", "no run at all"),
                ("a  b", "a  b", "not between tags"),
                ("<a> ", "<a> ", "trailing run with no following tag (N4 handles edges)"),
                (" <a>", " <a>", "leading run with no preceding tag"),
                ("<a>  <b>  <c>", "<a><b><c>", "several runs in one pass"),
            };
            foreach (var (input, want, why) in vectors)
            {
                var got = Normalize.CollapseBetweenTags(input);
                if (!string.Equals(got, want, StringComparison.Ordinal))
                    failures.Add($"N3 vector ({why}): {Show(input)} -> {Show(got)}, expected {Show(want)}");
            }
            return vectors.Length;
        }

        /// <summary>N1 must reject a lone surrogate — output that cannot have come from valid UTF-8.</summary>
        private static int WellFormednessRejectsLoneSurrogate(List<string> failures)
        {
            var cases = new (string Text, bool ShouldThrow, string Why)[]
            {
                ("plain", false, "ASCII"),
                ("\uD83D\uDE00", false, "a valid surrogate pair"),
                ("\uD83D", true, "a lone high surrogate"),
                ("\uDE00", true, "a lone low surrogate"),
                ("a\uD83Db", true, "a high surrogate not followed by a low one"),
                ("\uFEFFtext", false, "a BOM is not stripped and is not an N1 failure"),
            };
            foreach (var (text, shouldThrow, why) in cases)
            {
                var threw = false;
                try { Normalize.AssertWellFormed(text, "selftest"); }
                catch (GateFailure) { threw = true; }
                if (threw != shouldThrow)
                    failures.Add($"N1 ({why}): expected {(shouldThrow ? "reject" : "accept")}, got {(threw ? "reject" : "accept")}");
            }
            return cases.Length;
        }

        private static int FirstDiff(string a, string b)
        {
            var n = Math.Min(a.Length, b.Length);
            for (var i = 0; i < n; i++)
                if (a[i] != b[i]) return i;
            return n;
        }

        private static string Show(string s)
            => "\"" + s.Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r") + "\"";
    }
}
