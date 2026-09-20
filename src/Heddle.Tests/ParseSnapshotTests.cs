using System;
using System.Collections.Generic;
using System.Text;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Language;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>What a hook sees of the document it is called from, recorded per call.</summary>
    [ExtensionName("chainswritten")]
    public sealed class ChainsWrittenExtension : AbstractExtension
    {
        public static readonly List<int> Observed = new List<int>();
        public static readonly List<string> Positions = new List<string>();

        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            lock (Observed)
            {
                Observed.Add(initContext.ParseContext.OutputChains.Count);
                foreach (var chain in initContext.ParseContext.OutputChains)
                    Positions.Add(chain.BlockPosition.StartIndex + "+" + chain.BlockPosition.Length);
            }
            return base.InitStart(initContext, dataType, chainedType, parent);
        }

        public override object ProcessData(in Scope scope) => string.Empty;

        public override void RenderData(in Scope scope)
        {
        }
    }

    /// <summary>Every output chain keeps an isolated view of the context it was written in. These pin what that
    /// view costs and what it shows, which pull against each other: it has to be cheap enough to take once per
    /// chain, and it still has to show a hook everything it always showed.</summary>
    public class ParseSnapshotTests
    {
        public sealed class Model
        {
            public string Title { get; set; } = "t";
        }

        /// <summary>A bodiless call's hook reads the document through <c>InitContext.ParseContext</c>, and what
        /// it finds there is the document as written up to the call: the chains before it, not the ones after.
        /// Pins the regression where making the view cheap emptied it.</summary>
        [Fact]
        public void AHookSeesTheChainsWrittenBeforeItsCall()
        {
            TemplateFactory.AddExtensions(TemplateFactory.LoadExtensions(new[] { typeof(ChainsWrittenExtension) }));
            lock (ChainsWrittenExtension.Observed)
                ChainsWrittenExtension.Observed.Clear();

            var template = new HeddleTemplate("@(Title)@chainswritten()@(Title)@(Title)@chainswritten()@(Title)",
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text }, typeof(Model)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            lock (ChainsWrittenExtension.Observed)
                Assert.Equal(new[] { 1, 4 }, ChainsWrittenExtension.Observed.ToArray());
        }

        /// <summary>An import's chains are moved into the importing document once the import is parsed, and
        /// given the import's place there. A hook called inside the import reads the chains before it through
        /// a view taken while they were still where the import's own text has them, and that is what it finds
        /// — not where they were moved to afterwards, before the view was first read.</summary>
        [Fact]
        public void AHookInsideAnImportSeesTheImportsChainsWhereTheImportHasThem()
        {
            const string library = "@(Title)-@(Title)@chainswritten()";
            TemplateFactory.AddExtensions(TemplateFactory.LoadExtensions(new[] { typeof(ChainsWrittenExtension) }));
            var alone = DocumentParser.Parse(library, new ParserSettings(), out _);
            var expected = new List<string>();
            for (int i = 0; i < 2; i++)
                expected.Add(alone.OutputChains[i].BlockPosition.StartIndex + "+" + alone.OutputChains[i].BlockPosition.Length);

            lock (ChainsWrittenExtension.Observed)
            {
                ChainsWrittenExtension.Positions.Clear();
                var context = new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text }, typeof(Model));
                context.ImportReader = path => library;
                var template = new HeddleTemplate("xx@<<{{lib.heddle}}yy", context);
                Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
                Assert.Equal(expected, ChainsWrittenExtension.Positions);
            }
        }

        private static string Library(int definitions, int calls)
        {
            var text = new StringBuilder("@%\n");
            for (int i = 0; i < definitions; i++)
                text.Append("<card").Append(i).Append(">{{<div>@(Title) @(Title)</div>}}\n");
            text.Append("%@\n");
            for (int i = 0; i < calls; i++)
                text.Append("<p>@card").Append(i % definitions).Append("()</p>\n");
            return text.ToString();
        }

        private static long CompileAllocatedBytes(string document)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var template = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text }, typeof(Model)));
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());
            return allocated;
        }

        /// <summary>A page over a component library: many definitions, many calls. Pins the growth: doubling
        /// both must not cost what it did when every chain copied every definition's body — eight times and
        /// more. What is measured is what the compile allocates, not how long it takes: the copies were the
        /// cost, allocation counts them exactly, and the number is the same on a loaded two-core runner as on
        /// an idle workstation, which no timing is. Both documents are compiled once first, so nothing the
        /// first compile of a process allocates is in either figure.</summary>
        [Fact]
        public void DoublingDefinitionsAndCallsDoesNotMultiplyTheCompileByTheSquare()
        {
            string small = Library(20, 80);
            string large = Library(40, 160);
            CompileAllocatedBytes(small);
            CompileAllocatedBytes(large);

            long smallBytes = CompileAllocatedBytes(small);
            long largeBytes = CompileAllocatedBytes(large);

            Assert.True(largeBytes <= smallBytes * 3,
                $"20 definitions and 80 calls allocated {smallBytes:N0} bytes to compile, twice that {largeBytes:N0} — a ratio of {(double) largeBytes / smallBytes:F1}; linear growth is 2, and the regression this pins was above 8.");
            Assert.True(largeBytes <= CeilingBytes,
                $"40 definitions and 160 calls allocated {largeBytes:N0} bytes to compile; the ceiling is {CeilingBytes:N0}.");
        }

        private const long CeilingBytes = 16L * 1024 * 1024;

        private static string Compile(string document, out HeddleTemplate template)
        {
            template = new HeddleTemplate(document,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text }, typeof(Model)));
            var text = new StringBuilder();
            foreach (var error in template.CompileResult.Errors)
                text.Append(error.DiagnosticId).Append('@').Append(error.Position.StartIndex).Append('+')
                    .Append(error.Position.Length).Append(' ');
            if (template.CompileResult.Success)
                text.Append("=> ").Append(template.Generate(new Model()));
            return text.ToString();
        }

        /// <summary>A call binds the definition it names as the document stood at the call, the whole base chain
        /// included. Here the base <c>d1</c> gains a required prop after the first call, and the derived
        /// <c>d2</c> is then overridden too — which keeps the old <c>d2</c> as a copy made after <c>d1</c>
        /// changed. The first call must still see <c>d2</c> over the old <c>d1</c>, and compile.</summary>
        [Fact]
        public void ACallSeesItsBaseChainAsItStoodEvenAfterBothLinksAreOverridden()
        {
            string result = Compile(
                "@%<d1>{{OLD}} <d2:d1>{{two}}%@[@d2()]@%<d1(size: string):d1>{{NEW @(size)}}%@@%<d2:d2>{{two2}}%@[@d2(size: \"x\")]",
                out _);
            Assert.Equal("=> [two][two2]", result);
        }

        /// <summary>The same through a slot: the base gains a typed slot after the first call, which passes a
        /// body that the untyped slot of the time accepts.</summary>
        [Fact]
        public void ACallSeesItsBaseSlotAsItStoodEvenAfterBothLinksAreOverridden()
        {
            string result = Compile(
                "@%<b>{{B}}<c:b>{{C@out()}}%@@c(){{body}}|@%<b(out:: string):b>{{B2}}%@@%<c:c>{{C2@c(){{@out()}}}}%@",
                out _);
            Assert.Equal("=> Cbody|", result);
        }

        /// <summary>The reverse: a diagnostic that must keep firing. The first call passes a prop nothing
        /// declared at the time; that the base declares it later does not make the call valid.</summary>
        [Fact]
        public void ACallIsNotValidatedAgainstPropsItsBaseGainedLater()
        {
            string result = Compile(
                "@%<b>{{B}}<c:b>{{C}}%@@c(extra: \"zz\")|@%<b(extra: string = \"x\"):b>{{B2[@(extra)]}}%@@%<c:c>{{C2@c()}}%@@c(extra: \"q\")",
                out _);
            Assert.Equal("HED5006@25+11 ", result);
        }

        private static void Describe(ParseContext context, StringBuilder text, int depth, HashSet<ParseContext> path)
        {
            text.Append("{raws=").Append(context.RawOutputItems.Count).Append(" skipped=")
                .Append(context.SkippedTokens.Count).Append(" defpos=");
            foreach (var position in context.DefinitionsBlock.Positions)
                text.Append(position.StartIndex).Append('+').Append(position.Length).Append(',');
            if (depth == 0 || !path.Add(context))
            {
                text.Append("}");
                return;
            }

            foreach (var pair in context.DefinitionsBlock.Definitions)
            {
                text.Append(" def ").Append(pair.Key).Append('=');
                for (var item = pair.Value; item != null; item = item.BaseDefinition)
                {
                    text.Append(item.Name).Append('(').Append(item.ParameterTemplate).Append(')')
                        .Append(item.Position.StartIndex).Append('+').Append(item.Position.Length)
                        .Append(" props=").Append(item.PropDeclarations.Count).Append(" slot=").Append(item.SlotTypeName);
                    Describe(item.Context, text, depth - 1, path);
                    text.Append("<-");
                }
            }

            foreach (var chains in new[] { context.OutputChains, context.DefaultChains })
            {
                text.Append(" chains:");
                foreach (var chain in chains)
                {
                    text.Append(chain.BlockPosition.StartIndex).Append('+').Append(chain.BlockPosition.Length);
                    foreach (var item in chain.Chain)
                    {
                        text.Append(' ').Append(item.ExtensionName);
                        if (item.Context != null)
                            Describe(item.Context, text, depth - 1, path);
                    }

                    if (!ReferenceEquals(chain.Context, context))
                        Describe(chain.Context, text, depth - 1, path);
                    text.Append(';');
                }
            }

            path.Remove(context);
            text.Append("}");
        }

        /// <summary>A view is read late, and a host may have edited the document in between. What it set on a
        /// definition, and what it appended to a definition's body, after a chain was written is not part of
        /// what that chain saw.</summary>
        [Fact]
        public void AHostsLaterEditsToADefinitionAreNotSeenThroughAnEarlierChainsView()
        {
            var root = DocumentParser.Parse("@%<a>{{A@a2()}}<a2>{{X}}%@@a()@a()", new ParserSettings(), out _);
            var a = root.GetDefenition("a");
            var position = a.Position;
            a.Position = new Heddle.Strings.Core.BlockPosition(3, 3);
            a.Context.OutputChains.Add(new OutputChain(a.Context));
            a.Context.RawOutputItems.Add(new RawOutputItem { Text = "late" });
            a.Context.DefinitionsBlock.Definitions["late"] = new DefinitionItem("late", null, null);

            foreach (var chain in root.OutputChains)
            {
                var seen = chain.Context.GetDefenition("a");
                Assert.Equal(position, seen.Position);
                Assert.Single(seen.Context.OutputChains);
                Assert.Empty(seen.Context.RawOutputItems);
                Assert.False(seen.Context.DefenitionExists("late"));
            }

            var replaced = DocumentParser.Parse("@%<a>{{A@a2()}}<a2>{{X}}%@@a()", new ParserSettings(), out _);
            replaced.GetDefenition("a").Context = new ParseContext();
            Assert.Single(replaced.OutputChains[0].Context.GetDefenition("a").Context.OutputChains);
        }

        private static readonly string[] Pieces =
        {
            "@%<a>{{A}}%@", "@%<a:a>{{A2@a()}}%@", "@%<a(size: string):a>{{A3 @(size)}}%@",
            "@%<a(out:: string):a>{{A4}}%@", "@%<b:a>{{B@out()}}%@", "@%<b:b>{{B2@b()}}%@", "@%<c:b>{{C}}%@",
            "@%<c:c>{{C2@c(){{@out()}}}}%@", "@%<b(extra: string = \"x\"):b>{{B3[@(extra)]}}%@",
            "@a()", "@b()", "@c()", "@a(size: \"s\")", "@b(){{body}}", "@c(extra: \"e\")", "@c(){{@a()}}",
            "[@(Title)]", " text ", "@chainswritten()"
        };

        /// <summary>What an output chain's view of the document shows must not depend on when it is read. Several
        /// hundred small documents — definitions, derivations, in-place overrides, calls, in a seeded random
        /// order — are each compiled twice: once as always, the views read afterwards, and once with every view
        /// read in full the moment it is taken, which is what copying eagerly did. Diagnostics, output and the
        /// whole parse tree must agree.</summary>
        [Fact]
        public void WhatAViewShowsDoesNotDependOnWhenItIsRead()
        {
            TemplateFactory.AddExtensions(TemplateFactory.LoadExtensions(new[] { typeof(ChainsWrittenExtension) }));
            var random = new Random(20260920);
            for (int i = 0; i < 300; i++)
            {
                var document = new StringBuilder();
                // Half of them over a ready three-level chain, so overrides of a base with derived definitions
                // above it — the interesting case — are common rather than a rare draw.
                if (i % 2 == 0)
                    document.Append(Pieces[0]).Append(Pieces[4]).Append(Pieces[6]);
                int pieces = 3 + random.Next(9);
                for (int k = 0; k < pieces; k++)
                    document.Append(Pieces[random.Next(Pieces.Length)]);
                string text = document.ToString();

                string late = Compile(text, out var lateTemplate);
                var lateTree = new StringBuilder();
                Describe(lateTemplate.CompileResult.Context, lateTree, 4, new HashSet<ParseContext>());

                string atOnce;
                var atOnceTree = new StringBuilder();
                ParseContext.ReadIsolationsAtOnce = true;
                try
                {
                    atOnce = Compile(text, out var atOnceTemplate);
                    ParseContext.ReadIsolationsAtOnce = false;
                    Describe(atOnceTemplate.CompileResult.Context, atOnceTree, 4, new HashSet<ParseContext>());
                }
                finally
                {
                    ParseContext.ReadIsolationsAtOnce = false;
                }

                Assert.True(atOnce == late, "document " + i + ": " + text + "\n at once: " + atOnce + "\n late:    " + late);
                Assert.True(atOnceTree.ToString() == lateTree.ToString(), "document " + i + ": " + text);
            }
        }

        /// <summary>A compiled template's parse tree is public, and reading it completes the views its chains
        /// keep. Several threads reading one tree at once must all find the same thing, and none may fault.</summary>
        [Fact]
        public void SeveralThreadsMayReadOneParseTreeAtOnce()
        {
            Compile(Library(12, 48), out var reference);
            var expected = new StringBuilder();
            Describe(reference.CompileResult.Context, expected, 3, new HashSet<ParseContext>());

            for (int round = 0; round < 5; round++)
            {
                Compile(Library(12, 48), out var template);
                var failures = new List<string>();
                var start = new System.Threading.ManualResetEventSlim();
                var threads = new List<System.Threading.Thread>();
                for (int n = 0; n < 8; n++)
                {
                    var thread = new System.Threading.Thread(() =>
                    {
                        start.Wait();
                        try
                        {
                            var found = new StringBuilder();
                            Describe(template.CompileResult.Context, found, 3, new HashSet<ParseContext>());
                            if (found.ToString() != expected.ToString())
                                lock (failures) failures.Add("a thread read a different tree");
                        }
                        catch (Exception e)
                        {
                            lock (failures) failures.Add(e.ToString());
                        }
                    });
                    thread.Start();
                    threads.Add(thread);
                }

                start.Set();
                foreach (var thread in threads)
                    thread.Join();
                Assert.True(failures.Count == 0, failures.Count + " of 8 readers failed; first: " +
                    (failures.Count > 0 ? failures[0] : null));
            }
        }
    }
}
