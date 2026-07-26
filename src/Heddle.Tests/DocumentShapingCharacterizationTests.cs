using System.Collections.Generic;
using System.Linq;
using Heddle.Language;
using Heddle.Strings.Core;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>Generator plan phase 2 WI2 — the machine-level characterization pins for the shared document-shaping
    /// core (<see cref="DocumentShaping"/>). Every expectation in this file was <b>captured from the runtime
    /// implementation before the extraction moved it</b> (the pre-swap <c>HeddleCompiler</c> privates, driven
    /// through reflection over the same vector table); the extraction is byte-neutral exactly when these literals
    /// still hold. They are the definition of "byte-neutral" at machine granularity — never regenerate one to
    /// absorb a diff.</para>
    /// <para><see cref="Heddle.Generator.Tests"/> carries the identical twin over the generator's linked copy of
    /// this file, with the same literals: that pair is the parity pin.</para>
    /// </summary>
    public class DocumentShapingCharacterizationTests
    {
        // ---- vector plumbing ----

        internal static ParseContext Ctx(
            (int start, int length)[] chains = null,
            (int start, int length)[] definitions = null,
            (int start, int length)[] raws = null,
            (int start, int length)[] skipped = null)
        {
            var context = new ParseContext();
            foreach (var c in chains ?? new (int, int)[0])
                context.OutputChains.Add(new OutputChain(context) { BlockPosition = new BlockPosition(c.start, c.length) });
            foreach (var d in definitions ?? new (int, int)[0])
                context.DefinitionsBlock.Positions.Add(new BlockPosition(d.start, d.length));
            foreach (var r in raws ?? new (int, int)[0])
                context.RawOutputItems.Add(new RawOutputItem { BlockPosition = new BlockPosition(r.start, r.length), Text = "R" });
            foreach (var s in skipped ?? new (int, int)[0])
                context.SkippedTokens.Add(new BlockPosition(s.start, s.length));
            return context;
        }

        private static string Positions(IEnumerable<BlockPosition> positions)
            => string.Join(",", positions.Select(p => p.StartIndex + "+" + p.Length));

        internal static string Snapshot(ParseContext context, string document)
            => "doc=[" + document.Replace("\r", "\\r").Replace("\n", "\\n") + "]"
               + " chains=[" + Positions(context.OutputChains.Select(c => c.BlockPosition)) + "]"
               + " defs=[" + Positions(context.DefinitionsBlock.Positions) + "]"
               + " raws=[" + Positions(context.RawOutputItems.Select(r => r.BlockPosition)) + "]";

        // ---- pin 1: ShiftBySkippedTokens, three-way over all three lists ----

        [Fact]
        public void Pin1_ShiftBySkippedTokens_ThreeWayOverEveryList()
        {
            var context = Ctx(
                chains: new[] { (0, 3), (8, 10), (20, 5), (28, 6), (40, 2) },
                definitions: new[] { (0, 2), (9, 8), (22, 3) },
                raws: new[] { (0, 2), (20, 4), (35, 1) },
                skipped: new[] { (10, 4), (30, 2) });

            DocumentShaping.ShiftBySkippedTokens(context);

            Assert.Equal("doc=[] chains=[0+3,8+6,16+5,24+4,34+2] defs=[0+2,9+4,18+3] raws=[0+2,16+4,29+1]",
                Snapshot(context, ""));
        }

        /// <summary>
        /// Pin 1, boundary rows (added by the phase-2 restoration audit, 2026-07-26). The captured vector above
        /// exercises the three-way classification but never at its <em>boundaries</em>: no block in it starts
        /// exactly at a skipped token's start, ends exactly at its end, or ends exactly at its start. Mutation
        /// testing confirmed six single-comparison mutants of the classification survived it
        /// (<c>chainBlockStart &lt;= startToSkip</c> → <c>&lt;</c>, <c>chainBlockEnd &gt;= endToSkip</c> →
        /// <c>&gt;</c>, both again for the definitions list, the raw list's <c>&gt;</c>, and the chain
        /// after-shift <c>&gt;</c>). One row per boundary, each derived from the runtime body's own predicates.
        /// </summary>
        [Theory]
        // enclosing at the exact LEFT boundary: block start == skipped start → keeps its start, loses the length
        [InlineData(new[] { 10, 4 }, new[] { 10, 8 }, new int[0], new int[0],
            "doc=[] chains=[10+4] defs=[] raws=[]")]
        // enclosing at the exact RIGHT boundary: block end == skipped end → still enclosing, not a shift
        [InlineData(new[] { 10, 4 }, new[] { 8, 6 }, new int[0], new int[0],
            "doc=[] chains=[8+2] defs=[] raws=[]")]
        // the after-shift predicate's boundary: block end == skipped start → wholly before, untouched, loop breaks
        [InlineData(new[] { 10, 4 }, new[] { 9, 2 }, new int[0], new int[0],
            "doc=[] chains=[9+2] defs=[] raws=[]")]
        // the definitions list carries the same two enclosing boundaries (here both at once: 10..13 == 10..13)
        [InlineData(new[] { 10, 4 }, new int[0], new[] { 10, 4 }, new int[0],
            "doc=[] chains=[] defs=[10+0] raws=[]")]
        // ... and the same wholly-before boundary
        [InlineData(new[] { 10, 4 }, new int[0], new[] { 9, 2 }, new int[0],
            "doc=[] chains=[] defs=[9+2] raws=[]")]
        // the raw list has no enclosing case at all — only the `end > skippedStart` shift and its boundary
        [InlineData(new[] { 10, 4 }, new int[0], new int[0], new[] { 8, 3, 9, 3 },
            "doc=[] chains=[] defs=[] raws=[8+3,5+3]")]
        public void Pin1_ShiftBySkippedTokens_ClassificationBoundaries(int[] skipped, int[] chains,
            int[] definitions, int[] raws, string expected)
        {
            var context = Ctx(chains: Pairs(chains), definitions: Pairs(definitions), raws: Pairs(raws),
                skipped: Pairs(skipped));

            DocumentShaping.ShiftBySkippedTokens(context);

            Assert.Equal(expected, Snapshot(context, ""));
        }

        // ---- pin 2: TrimHiddenRemnantLines ----

        [Theory]
        // whole-line comment remnant — removed
        [InlineData("A\n\nB\n", new[] { 2, 5 }, new[] { 0, 1, 3, 1 }, new int[0],
            "doc=[A\\nB\\n] chains=[0+1,2+1] defs=[] raws=[]")]
        // remnant line that retains content (the @\ shape) — no-op
        [InlineData("A\nX\nB\n", new[] { 2, 5 }, new[] { 0, 1 }, new int[0],
            "doc=[A\\nX\\nB\\n] chains=[0+1] defs=[] raws=[]")]
        // two hidden tokens on one line — removed once via the already-removed-span skip
        [InlineData("A\n\nB\n", new[] { 2, 3, 5, 2 }, new[] { 0, 1 }, new int[0],
            "doc=[A\\nB\\n] chains=[0+1] defs=[] raws=[]")]
        // remnant inside a definition block — the ShiftListsAfter enclosing case (the documented historical bug)
        [InlineData("A\n  \nB\n", new[] { 2, 4 }, new[] { 0, 1 }, new[] { 0, 6 },
            "doc=[A\\nB\\n] chains=[0+1] defs=[0+3] raws=[]")]
        // Added by the restoration audit (2026-07-26): the already-removed-span guard, actually constrained. The
        // row above ("two hidden tokens on one line") does NOT constrain it — after the first removal the second
        // probe lands on a line that retains content, so it declines on its own and deleting the guard changes
        // nothing (mutation-verified). Here BOTH tokens map to clean start 2 on a run of THREE blank lines, so
        // without the guard the second probe would eat a second line.
        [InlineData("A\n\n\nB\n", new[] { 2, 3, 5, 2 }, new[] { 0, 1 }, new int[0],
            "doc=[A\\n\\nB\\n] chains=[0+1] defs=[] raws=[]")]
        public void Pin2_TrimHiddenRemnantLines(string document, int[] skipped, int[] chains, int[] definitions,
            string expected)
        {
            var context = Ctx(chains: Pairs(chains), definitions: Pairs(definitions), skipped: Pairs(skipped));
            var working = document;

            DocumentShaping.TrimHiddenRemnantLines(context, ref working);

            Assert.Equal(expected, Snapshot(context, working));
        }

        /// <summary>
        /// Pin 2's <c>ShiftListsAfter</c> boundaries (added by the restoration audit, 2026-07-26). The row above
        /// pins the definitions-list enclosing arm — deleting that arm is red, which is the documented historical
        /// bug — but nothing pinned the <em>chain</em> list's enclosing arm or the exact boundary comparisons, and
        /// three mutants of them survived. Here the chain spans the removed remnant span exactly (both boundaries
        /// at once) and the two raw items straddle the raw list's <c>end &gt; removedStart</c> boundary.
        /// </summary>
        [Fact]
        public void Pin2_ShiftListsAfter_EnclosingAndShiftBoundaries()
        {
            var context = Ctx(chains: new[] { (2, 3) }, raws: new[] { (1, 2), (3, 2) }, skipped: new[] { (2, 4) });
            var working = "A\n  \nB\n";

            DocumentShaping.TrimHiddenRemnantLines(context, ref working);

            // The whitespace-only remnant line [2,5) is removed (seed 3); the chain enclosing it keeps its start
            // and loses the whole seed; the raw ending exactly at the removal's start is wholly before.
            Assert.Equal("doc=[A\\nB\\n] chains=[2+0] defs=[] raws=[1+2,0+2]", Snapshot(context, working));
        }

        // ---- pin 3: the WidenToWholeLine vector table (the WI1 extensional-equality pin) ----

        [Theory]
        [InlineData("  ab  \nX", 2, 2, 0, 7)]        // in-bounds whole line, LF terminator
        [InlineData("  ab  \r\nX", 2, 2, 0, 8)]      // CRLF pair
        [InlineData("  ab  \rX", 2, 2, 0, 7)]        // bare CR
        [InlineData("  ab  ", 2, 2, 0, 6)]           // EOF is a valid terminator
        [InlineData("z ab\nX", 2, 2, 2, 2)]          // content on the left — rejected
        [InlineData("  ab z\nX", 2, 2, 2, 2)]        // content on the right — rejected
        [InlineData("A\n  \nB", 2, 0, 2, 3)]         // zero-length probe on a whitespace-only line
        [InlineData("A\nxy\nB", 2, 0, 2, 0)]         // zero-length probe on a line with content
        [InlineData("ab", 7, 0, 2, 0)]               // start past end — clamped (WI1)
        [InlineData("  ab", 2, 99, 0, 4)]            // length past end — clamped (WI1)
        [InlineData("", 0, 0, 0, 0)]                 // empty document
        [InlineData("X\n", 5, 0, 2, 0)]              // start past end, terminator-adjacent — clamped (WI1)
        [InlineData("ab\ncd", 0, 2, 0, 3)]           // BOF counts as a left terminator
        public void Pin3_WidenToWholeLine(string document, int start, int length, int expectedStart,
            int expectedLength)
        {
            var widened = DocumentShaping.WidenToWholeLine(new BlockPosition(start, length), document);

            Assert.Equal(expectedStart, widened.StartIndex);
            Assert.Equal(expectedLength, widened.Length);
        }

        // ---- pin 4: RemoveDefinitions ----

        [Theory]
        // trimming off — exactly the block
        [InlineData("  <d>\nTail\n", new[] { 2, 3 }, new[] { 5, 1, 6, 4 }, new int[0], false,
            "doc=[  \\nTail\\n] chains=[2+1,3+4] defs=[2+3] raws=[]")]
        // trimming on — widened to the whole line, later blocks shift by the WIDENED length
        [InlineData("  <d>\nTail\n", new[] { 2, 3 }, new[] { 6, 4, 10, 1 }, new int[0], true,
            "doc=[Tail\\n] chains=[0+4,4+1] defs=[2+3] raws=[]")]
        // shift-predicate boundary: StartIndex == defStart+defLen shifts, one less does not (and breaks the loop)
        [InlineData("  <d>x<e>\n", new[] { 2, 3, 6, 3 }, new[] { 5, 1, 9, 1 }, new int[0], true,
            "doc=[  x\\n] chains=[2+1,3+1] defs=[2+3,6+3] raws=[]")]
        // the raw-output list rebases on the same predicate
        [InlineData("  <d>\nTail\n", new[] { 2, 3 }, new int[0], new[] { 6, 2 }, true,
            "doc=[Tail\\n] chains=[] defs=[2+3] raws=[0+2]")]
        public void Pin4_RemoveDefinitions(string document, int[] definitions, int[] chains, int[] raws, bool trim,
            string expected)
        {
            var context = Ctx(chains: Pairs(chains), definitions: Pairs(definitions), raws: Pairs(raws));
            var working = document;

            DocumentShaping.RemoveDefinitions(context, ref working, trim);

            Assert.Equal(expected, Snapshot(context, working));
        }

        // ---- pin 5: ReplaceRawOutput ----

        [Theory]
        // replacement shorter than the span; the chain exactly at the splice start is NOT shifted (`>`, not `>=`)
        [InlineData("AA[[RAW]]BB", 2, 7, "x", new[] { 2, 7, 9, 2 }, "doc=[AAxBB] chains=[2+7,3+2] defs=[] raws=[2+7]")]
        // replacement longer than the span — the shift is negative
        [InlineData("AA[[R]]BB", 2, 5, "LONGER", new[] { 2, 5, 7, 2 },
            "doc=[AALONGERBB] chains=[2+5,8+2] defs=[] raws=[2+5]")]
        public void Pin5_ReplaceRawOutput(string document, int rawStart, int rawLength, string text, int[] chains,
            string expected)
        {
            var context = Ctx(chains: Pairs(chains));
            context.RawOutputItems.Add(new RawOutputItem
            {
                BlockPosition = new BlockPosition(rawStart, rawLength), Text = text
            });
            var working = document;

            DocumentShaping.ReplaceRawOutput(context, ref working);

            Assert.Equal(expected, Snapshot(context, working));
        }

        // ---- pin 6: RemoveEmptyItem ----

        [Theory]
        [InlineData("  @m()  \nTail\n", 2, 5, new[] { 2, 5, 9, 4 }, false,
            "doc=[   \\nTail\\n] chains=[2+5,4+4] defs=[] raws=[]")]
        [InlineData("  @m()  \nTail\n", 2, 5, new[] { 2, 5, 9, 4 }, true,
            "doc=[Tail\\n] chains=[2+5,0+4] defs=[] raws=[]")]
        [InlineData("@m()\nTail\n", 0, 4, new[] { 0, 4, 5, 4 }, true,
            "doc=[Tail\\n] chains=[0+4,0+4] defs=[] raws=[]")]
        public void Pin6_RemoveEmptyItem(string document, int start, int length, int[] chains, bool trim,
            string expected)
        {
            var context = Ctx(chains: Pairs(chains));
            var working = document;

            DocumentShaping.RemoveEmptyItem(context, new BlockPosition(start, length), ref working, trim);

            Assert.Equal(expected, Snapshot(context, working));
        }

        // ---- pin 7: the branch-set strip machine ----

        [Theory]
        // opener → continuation → terminal: two gaps, applied right-to-left
        [InlineData("if  el  ee  ZZ", "if@0+2|elif@4+2|else@8+2", "", "doc=[ifelee  ZZ] chains=[0+2,2+2,4+2] defs=[] raws=[]")]
        // opener → other: disarms, so the terminal collects no gap
        [InlineData("if  xx  ee", "if@0+2|other@4+2|else@8+2", "", "doc=[if  xx  ee] chains=[0+2,4+2,8+2] defs=[] raws=[]")]
        // orphan continuation: no armed opener, so no gap before it (the diagnostics are the runtime's, pinned
        // separately by the branch suites)
        [InlineData("xx  el  yy", "other@0+2|elif@4+2|else@8+2", "", "doc=[xx  elyy] chains=[0+2,4+2,6+2] defs=[] raws=[]")]
        // a Participant between opener and continuation disarms — no gap across it
        [InlineData("if  pp  el", "if@0+2|part@4+2|elif@8+2", "", "doc=[if  pp  el] chains=[0+2,4+2,8+2] defs=[] raws=[]")]
        // zero-length gap (adjacent blocks) is skipped
        [InlineData("ifel  ee", "if@0+2|elif@2+2|else@6+2", "", "doc=[ifelee] chains=[0+2,2+2,4+2] defs=[] raws=[]")]
        // definition-shadowed keyword (R8) classifies Other, so the set never forms
        [InlineData("if  el  ee  ZZ", "if@0+2|elif@4+2|else@8+2", "elif", "doc=[if  el  ee  ZZ] chains=[0+2,4+2,8+2] defs=[] raws=[]")]
        public void Pin7_StripBranchSets(string document, string names, string definitionNames, string expected)
        {
            var context = new ParseContext();
            foreach (var part in names.Split('|'))
            {
                var name = part.Substring(0, part.IndexOf('@'));
                var span = part.Substring(part.IndexOf('@') + 1).Split('+');
                AddNamed(context, name, int.Parse(span[0]), int.Parse(span[1]));
            }

            var definitions = new HashSet<string>(definitionNames.Split(new[] { '|' },
                System.StringSplitOptions.RemoveEmptyEntries));
            var working = document;
            DocumentShaping.StripBranchSets(context, ref working, chain => Classify(chain, definitions));

            Assert.Equal(expected, Snapshot(context, working));
        }

        [Fact] // negative gap (an imported zero-length block inside the previous one) and the bounds guard
        public void Pin7_StripBranchSets_NonPositiveGapAndBoundsGuard()
        {
            var negative = new ParseContext();
            AddNamed(negative, "if", 0, 4);
            AddNamed(negative, "elif", 2, 2);
            var negativeDoc = "ifelee";
            DocumentShaping.StripBranchSets(negative, ref negativeDoc, c => Classify(c, new HashSet<string>()));
            Assert.Equal("doc=[ifelee] chains=[0+4,2+2] defs=[] raws=[]", Snapshot(negative, negativeDoc));

            var outOfBounds = new ParseContext();
            AddNamed(outOfBounds, "if", 0, 2);
            AddNamed(outOfBounds, "elif", 40, 2);
            var shortDoc = "if  el";
            DocumentShaping.StripBranchSets(outOfBounds, ref shortDoc, c => Classify(c, new HashSet<string>()));
            Assert.Equal("doc=[if  el] chains=[0+2,40+2] defs=[] raws=[]", Snapshot(outOfBounds, shortDoc));
        }

        /// <summary>
        /// <para>Pin 7, the structural half (added by the restoration audit, 2026-07-26). The strip vectors above
        /// cannot see the divergence pin 7 exists for: the generator's pre-phase private enum had <b>four</b> kinds
        /// to the runtime's five, so a <c>[ScopeChannel]</c> non-role extension fell into <c>default:</c> instead of
        /// <c>Participant</c> — and since both arms disarm, no assertion over the working document can tell them
        /// apart. It is checkable only as shape: the enum has five kinds, defined once, here.</para>
        /// <para>Byte-equality of <c>Participant</c> and <c>Other</c> inside the strip machine is therefore the
        /// honest limit of the strip-level pin; what the collapse actually cost was the runtime's orphan state
        /// (<c>Participant</c> → <c>Unknown</c>, <c>Other</c> → unchanged), which reaches the drivers only through
        /// the observer's reported kind — pinned by the event-stream test below and, at template granularity, by
        /// <c>BranchSetCompilerTests.C18</c>.</para>
        /// </summary>
        [Fact]
        public void Pin7_BranchKindHasFiveKindsDefinedOnce()
        {
            Assert.Equal(
                new[] { "Other", "Opener", "Continuation", "Terminal", "Participant" },
                System.Enum.GetNames(typeof(DocumentShaping.BranchKind)));
        }

        /// <summary>
        /// Pin 7, the observer contract (added by the restoration audit, 2026-07-26). Every runtime HED300x
        /// diagnostic was re-hosted onto this event stream, and its <em>order</em> within one block — HED3005
        /// (classified) → HED3001 (gap) → HED3002/3/4 (completed) — is the reason the interface has three events
        /// rather than the plan's two. Nothing pinned that at machine granularity; the branch suites pin it only
        /// through the diagnostics it produces. This asserts the stream itself, including that a
        /// <c>[ScopeChannel]</c> non-role chain is reported as <c>Participant</c> and not <c>Other</c>.
        /// </summary>
        [Theory]
        // opener → continuation → terminal: classified before gap before completed, twice
        [InlineData("if  el  ee  ZZ", "if@0+2|elif@4+2|else@8+2",
            "C:if=Opener|B:if=Opener|C:elif=Continuation|G:if->elif@2+2[  ]|B:elif=Continuation|"
            + "C:else=Terminal|G:elif->else@6+2[  ]|B:else=Terminal")]
        // a [ScopeChannel] non-role chain: reported Participant (the four-kind collapse is red here), no gap across it
        [InlineData("if  pp  el", "if@0+2|part@4+2|elif@8+2",
            "C:if=Opener|B:if=Opener|C:part=Participant|B:part=Participant|C:elif=Continuation|B:elif=Continuation")]
        // a non-whitespace gap still reaches the observer with its text — the HED3001 trigger condition
        [InlineData("ifXYee", "if@0+2|else@4+2",
            "C:if=Opener|B:if=Opener|C:else=Terminal|G:if->else@2+2[XY]|B:else=Terminal")]
        public void Pin7_ObserverEventStreamAndOrdering(string document, string names, string expected)
        {
            var context = new ParseContext();
            foreach (var part in names.Split('|'))
            {
                var name = part.Substring(0, part.IndexOf('@'));
                var span = part.Substring(part.IndexOf('@') + 1).Split('+');
                AddNamed(context, name, int.Parse(span[0]), int.Parse(span[1]));
            }

            var recorder = new RecordingObserver();
            var working = document;
            DocumentShaping.StripBranchSets(context, ref working,
                chain => Classify(chain, new HashSet<string>()), recorder);

            Assert.Equal(expected, string.Join("|", recorder.Events));
        }

        internal sealed class RecordingObserver : DocumentShaping.IBranchStripObserver
        {
            internal List<string> Events { get; } = new List<string>();

            private static string Name(OutputItem item) => item == null ? "<null>" : item.ExtensionName;

            public void OnClassified(OutputChain chain, OutputItem leftmost, DocumentShaping.BranchKind kind)
                => Events.Add("C:" + Name(leftmost) + "=" + kind);

            public void OnGapCollected(OutputChain prev, OutputChain next, OutputItem nextLeftmost,
                BlockPosition gap, string gapText)
                => Events.Add("G:" + Name(prev.Chain[0]) + "->" + Name(nextLeftmost) + "@" + gap.StartIndex + "+"
                              + gap.Length + "[" + gapText + "]");

            public void OnBlockCompleted(OutputChain chain, OutputItem leftmost, DocumentShaping.BranchKind kind)
                => Events.Add("B:" + Name(leftmost) + "=" + kind);
        }

        private static void AddNamed(ParseContext context, string name, int start, int length)
        {
            var chain = new OutputChain(context) { BlockPosition = new BlockPosition(start, length) };
            chain.Chain.Add(new OutputItem(name, new BlockPosition(start, length)));
            context.OutputChains.Add(chain);
        }

        /// <summary>The vector table's stand-in for each backend's real classifier: the same rule shape (R8
        /// definition-first guard, then role, then <c>[ScopeChannel]</c> → Participant).</summary>
        internal static DocumentShaping.BranchKind Classify(OutputChain chain, HashSet<string> definitions)
        {
            var name = chain.Chain != null && chain.Chain.Count > 0 ? chain.Chain[0].ExtensionName : null;
            if (name == null || definitions.Contains(name))
                return DocumentShaping.BranchKind.Other;
            switch (name)
            {
                case "if": return DocumentShaping.BranchKind.Opener;
                case "elif": return DocumentShaping.BranchKind.Continuation;
                case "else": return DocumentShaping.BranchKind.Terminal;
                case "part": return DocumentShaping.BranchKind.Participant;
                default: return DocumentShaping.BranchKind.Other;
            }
        }

        // ---- pin 8: SlicePieces ----

        [Theory]
        // element at offset 0 — no leading piece; trailing remainder emitted
        [InlineData("ABCDEF", "0+2", "E0|CDEF")]
        // adjacent elements — no inter-piece
        [InlineData("ABCDEF", "0+2|2+2", "E0|E1|EF")]
        // leading, inter, and trailing pieces
        [InlineData("ABCDEF", "1+2|4+1", "A|E0|D|E1|F")]
        // empty element list — a single whole-document piece
        [InlineData("ABCDEF", "", "ABCDEF")]
        // zero-length element at the end (the empty default chain's shape) — trailing text becomes its leading piece
        [InlineData("ABCDEF", "6+0", "ABCDEF|E0")]
        public void Pin8_SlicePieces(string document, string spans, string expected)
        {
            var elements = spans.Length == 0
                ? new List<BlockPosition>()
                : spans.Split('|').Select(s => new BlockPosition(int.Parse(s.Split('+')[0]), int.Parse(s.Split('+')[1])))
                    .ToList();

            var emitted = new List<string>();
            var completed = DocumentShaping.SlicePieces(elements, p => p, document,
                piece => emitted.Add(piece),
                element => { emitted.Add("E" + elements.IndexOf(element)); return true; });

            Assert.True(completed);
            Assert.Equal(expected, string.Join("|", emitted));
        }

        [Fact] // the emitter's mid-walk degrade: a false onElement abandons the walk without an exception
        public void Pin8_SlicePieces_ShortCircuitsOnRefusal()
        {
            var elements = new List<BlockPosition> { new BlockPosition(1, 2), new BlockPosition(4, 1) };
            var emitted = new List<string>();

            var completed = DocumentShaping.SlicePieces(elements, p => p, "ABCDEF",
                piece => emitted.Add(piece),
                element => { emitted.Add("E"); return false; });

            Assert.False(completed);
            Assert.Equal("A|E", string.Join("|", emitted));
        }

        private static (int, int)[] Pairs(int[] flat)
        {
            var pairs = new (int, int)[flat.Length / 2];
            for (int i = 0; i < pairs.Length; i++)
                pairs[i] = (flat[i * 2], flat[i * 2 + 1]);
            return pairs;
        }
    }
}
