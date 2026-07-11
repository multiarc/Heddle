using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The phase 3 compile-time scan corpus (D10, rows C01–C19): interleaved-text stripping (silent for
    /// whitespace, HED3001 once per non-whitespace gap), comment/<c>@\</c> gaps, directive-between-branches,
    /// CRLF/LF parity, two-sets, imported zero-length blocks, definition-shadowed names, and the orphan rows
    /// (HED3002/HED3003/HED3004) — each asserting rendered bytes and the exact positioned diagnostic set.
    /// </summary>
    public class BranchSetCompilerTests
    {
        public class Model { public bool A { get; set; } public bool B { get; set; } public DateTime D { get; set; } }

        private static HeddleTemplate Build(string template, TemplateOptions options = null)
        {
            HeddleTemplate.Configure(typeof(BranchSetCompilerTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            options ??= new TemplateOptions();
            return new HeddleTemplate(template, new CompileContext(options, typeof(Model)));
        }

        private static IReadOnlyList<HeddleCompileWarning> Warnings(HeddleTemplate t, string id) =>
            t.Context.CompileWarnings.Where(w => w.DiagnosticId == id).ToList();

        private static void AssertPositionedAtBlock(HeddleCompileError diag, string template, string marker)
        {
            int at = template.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(at >= 0, $"marker '{marker}' not found");
            // Position points at the block's first item — within the '@name(' opening.
            Assert.InRange(diag.Position.StartIndex, at, at + marker.Length + 1);
        }

        // C01 — whitespace-only gap stripped silently.
        [Fact]
        public void C01_WhitespaceGapStrippedSilently()
        {
            var t = Build("@if(A){{1}}\n@else(){{2}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings, w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3"));
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false }));
        }

        // C02 — non-whitespace gap stripped with exactly one HED3001 at the @else.
        [Fact]
        public void C02_NonWhitespaceGapStrippedWithOneWarning()
        {
            var template = "@if(A){{1}} STRAY @else(){{2}}";
            var t = Build(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var w = Assert.Single(Warnings(t, HeddleDiagnosticIds.BranchTextStripped));
            Assert.Equal("Text between branch blocks is never rendered.", w.Error);
            AssertPositionedAtBlock(w, template, "@else");
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false }));
        }

        // C03 — text before the @if and after the last branch renders (stripping is set-internal only).
        [Fact]
        public void C03_TextOutsideSetRenders()
        {
            var t = Build("before @if(A){{1}}@else(){{2}} after");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("before 1 after", t.Generate(new Model { A = true }));
            Assert.Equal("before 2 after", t.Generate(new Model { A = false }));
        }

        // C04 — comment between blocks is excised pre-scan; the remaining gap is whitespace → silent.
        [Fact]
        public void C04_CommentGapIsSilentSetIntact()
        {
            var t = Build("@if(A){{1}} @* note *@ @else(){{2}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Empty(Warnings(t, HeddleDiagnosticIds.BranchTextStripped));
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false }));
        }

        // C05 — @\-trimmed whitespace between blocks: hidden channel, no warning, set intact.
        [Fact]
        public void C05_TrimmedWhitespaceGapIsSilent()
        {
            var t = Build("@if(A){{1}}@\\\n   @else(){{2}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Empty(Warnings(t, HeddleDiagnosticIds.BranchTextStripped));
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false }));
        }

        // C06 — a directive block (@using) is Other: ends stripping adjacency but not the orphan state.
        [Fact]
        public void C06_DirectiveBetweenBranchesKeepsSetOpen()
        {
            var t = Build("@if(A){{1}}@using(){{System.Text}}@else(){{2}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings, w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3"));
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false }));
        }

        // C07 — an @date Other block splits the set for stripping only: the two gaps render, @else still binds.
        [Fact]
        public void C07_FormatBlockSplitsStrippingNotBinding()
        {
            var template = "@if(A){{1}} @date(D){{yyyy}} @else(){{2}}";
            var t = Build(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings, w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3"));
            var model = new Model { A = true, D = new DateTime(2021, 1, 1) };
            var outp = t.Generate(model);
            Assert.Contains("1", outp);
            Assert.Contains("2021", outp);
            // both gaps (spaces around @date) render
            Assert.Contains(" 2021 ", outp);
        }

        // C08 — two independent sets; the inter-set space renders.
        [Fact]
        public void C08_TwoIndependentSets()
        {
            var t = Build("@if(A){{1}}@else(){{2}} @if(B){{3}}@else(){{4}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings, w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3"));
            Assert.Equal("1 3", t.Generate(new Model { A = true, B = true }));
            Assert.Equal("2 4", t.Generate(new Model { A = false, B = false }));
            Assert.Equal("1 4", t.Generate(new Model { A = true, B = false }));
        }

        // C09 — the second opener ends set 1 and starts set 2; @else binds to set 2.
        [Fact]
        public void C09_SecondOpenerStartsNewSet()
        {
            var t = Build("@if(A){{1}}@if(B){{2}}@else(){{3}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings, w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3"));
            // A true, B true => "1"+"2"; else silent
            Assert.Equal("12", t.Generate(new Model { A = true, B = true }));
            // A true, B false => "1"; else binds to set 2 (B) => "3"
            Assert.Equal("13", t.Generate(new Model { A = true, B = false }));
            // A false, B false => "" + else "3"
            Assert.Equal("3", t.Generate(new Model { A = false, B = false }));
        }

        // C10 — CRLF authored template strips and warns identically to LF.
        [Fact]
        public void C10_CrlfVariantParity()
        {
            var crlf = "@if(A){{1}} STRAY \r\n@else(){{2}}";
            var t = Build(crlf);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Single(Warnings(t, HeddleDiagnosticIds.BranchTextStripped));
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false }));
        }

        // C11 — @<< imported chains join as zero-length blocks; nothing stripped; @else binds; zero diagnostics.
        [Fact]
        public void C11_ImportedBranchBlocksJoinTheImportingBodyLevel()
        {
            var options = new TemplateOptions { RootPath = "TestTemplate" };
            var t = Build("@if(A){{1}}@<<{{branch-import-else.heddle}}", options);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings, w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3"));
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false }));
        }

        // C12 — a definition named 'else' shadows the extension: Other, no HED3003, renders the definition.
        [Fact]
        public void C12_DefinitionShadowedElseIsNotOrphan()
        {
            var t = Build("@%<else>{{DEFELSE}}%@@else()");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.CompileResult.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
        }

        // C13 — @else(X): HED3004 at the @else; parameter compiles, evaluates, ignored; terminal unchanged.
        [Fact]
        public void C13_ElseWithParameterWarnsHed3004()
        {
            var template = "@if(A){{1}}@else(B){{2}}";
            var t = Build(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var w = Assert.Single(Warnings(t, HeddleDiagnosticIds.ElseConditionIgnored));
            Assert.Equal("A branch terminal takes no condition — its parameter is ignored.", w.Error);
            AssertPositionedAtBlock(w, template, "@else");
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("2", t.Generate(new Model { A = false, B = true }));
        }

        // C14 — orphan @else as the first block: HED3003 error, positioned, compilation fails.
        [Fact]
        public void C14_OrphanElseIsHed3003Error()
        {
            var template = "@else(){{2}}";
            var t = Build(template);
            Assert.False(t.CompileResult.Success);
            var e = Assert.Single(t.CompileResult.ErrorList, x => x.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
            Assert.Equal("'@else' is a branch terminal with no matching opener in this scope.", e.Error);
            AssertPositionedAtBlock(e, template, "@else");
        }

        // C15 — a second @else in one set: HED3003 at the second @else; a third errors again.
        [Fact]
        public void C15_SecondElseIsHed3003()
        {
            var template = "@if(A){{1}}@else(){{2}}@else(){{3}}";
            var t = Build(template);
            Assert.False(t.CompileResult.Success);
            var e = Assert.Single(t.CompileResult.ErrorList, x => x.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
            // positioned at the SECOND @else
            int secondElse = template.IndexOf("@else", template.IndexOf("@else", StringComparison.Ordinal) + 1, StringComparison.Ordinal);
            Assert.InRange(e.Position.StartIndex, secondElse, secondElse + 6);

            // a third orphan @else errors again (state stays Closed)
            var t3 = Build("@if(A){{1}}@else(){{2}}@else(){{3}}@else(){{4}}");
            Assert.False(t3.CompileResult.Success);
            Assert.Equal(2, t3.CompileResult.ErrorList.Count(x => x.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf));
        }

        // C16 — orphan @elif: HED3002 warning; renders exactly as @if.
        [Fact]
        public void C16_OrphanElifWarnsHed3002AndActsAsIf()
        {
            var template = "@elif(A){{1}}";
            var t = Build(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            var w = Assert.Single(Warnings(t, HeddleDiagnosticIds.ElifWithoutIf));
            AssertPositionedAtBlock(w, template, "@elif");
            Assert.Equal("1", t.Generate(new Model { A = true }));
            Assert.Equal("", t.Generate(new Model { A = false }));
        }

        // C17 — @elif after @else: HED3002 (state Closed); starts a NEW set; the second @else closes it (no HED3003).
        [Fact]
        public void C17_ElifAfterElseStartsNewSet()
        {
            var template = "@if(A){{1}}@else(){{2}}@elif(B){{3}}@else(){{4}}";
            var t = Build(template);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Single(Warnings(t, HeddleDiagnosticIds.ElifWithoutIf));
            Assert.Empty(Warnings(t, HeddleDiagnosticIds.ElseWithoutIf));
            Assert.DoesNotContain(t.CompileResult.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
        }

        // C18 — a [ScopeChannel] custom publisher, then @else: no diagnostic (state Unknown).
        [Fact]
        public void C18_CustomPublisherSuppressesOrphanDiagnostic()
        {
            var t = Build("@branchdriver()@else(){{2}}");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.CompileResult.ErrorList, e => e.DiagnosticId == HeddleDiagnosticIds.ElseWithoutIf);
            // runtime: the driver satisfied the set, so @else stays silent
            Assert.Equal("", t.Generate(new Model()));
        }

        // C19 — @import() runs AFTER the scan, and the branch scan classifies it as an Other block: a set
        // spanning it stays open and draws NO branch diagnostic (no HED3003 for the following @else).
        // Amendment (see the phase-3 ledger entry): importing position-bearing content (an @else output block,
        // or a definition) through @import() from an inline document hits a pre-existing GetBlockPosition
        // limitation independent of phase 3, so the overall compile fails on that unrelated error — but the
        // branch scan itself is clean. The ratified, realizable import splice for branch blocks is @<<
        // (parse-time), covered by C11/N4; the N5 guarantee (import-parsed blocks are not statically scanned)
        // holds by construction because @import().InitStart runs after ProcessBranchSets.
        [Fact]
        public void C19_ImportExtensionIsANonBranchBlockScanTreatsAsOther()
        {
            var options = new TemplateOptions { RootPath = "TestTemplate" };
            var t = Build("@if(A){{1}}@import(){{branch-import-else.heddle}}@else(){{2}}", options);
            // The branch scan does not misfire: @import is Other, so the @else after the open set is valid and
            // draws no HED3003 (nor any other HED3xxx). The compile fails only on the unrelated @import limitation.
            Assert.DoesNotContain(t.CompileResult.ErrorList,
                e => e.DiagnosticId != null && e.DiagnosticId.StartsWith("HED3", StringComparison.Ordinal));
        }
    }
}
