using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;
using Heddle.TestCorpus;

namespace Heddle.Tests
{
    /// <summary>
    /// Flagship set renders exactly one branch per condition state with byte-identical header/footer;
    /// interleaved variant renders byte-identically with one HED3001 per stripped gap; tests per-iteration
    /// isolation inside <c>@list</c> and nested-set independence. Line endings normalized per testing standards.
    /// </summary>
    public class BranchingGoldenTests
    {
        public class FlagshipModel { public bool IsFeatured { get; set; } public bool IsArchived { get; set; } }
        public class Item { public bool IsFeatured { get; set; } public bool IsArchived { get; set; } }
        public class ListModel { public List<Item> Items { get; set; } }
        public class NestedModel { public bool Outer { get; set; } public bool InnerA { get; set; } }

        private static void AssertGolden(string name, string actual)
        {
            File.WriteAllText(TestCorpusIndex.WrittenArtifactPath($"test-{name}.html"), actual);
            var expected = File.ReadAllText($"TestTemplate/generated-{name}.html").Replace("\r\n", "\n");
            Assert.Equal(expected, actual.Replace("\r\n", "\n"));
        }

        private static HeddleTemplate Compile(string fixture, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(BranchingGoldenTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var document = File.ReadAllText($"TestTemplate/{fixture}.heddle").Replace("\r\n", "\n");
            var t = new HeddleTemplate(document, new CompileContext(modelType));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t;
        }

        [Fact]
        public void FlagshipFeatured()
        {
            var t = Compile("branching-flagship", typeof(FlagshipModel));
            Assert.DoesNotContain(t.Context.CompileWarnings, w => w.DiagnosticId != null && w.DiagnosticId.StartsWith("HED3"));
            AssertGolden("branching-flagship-featured", t.Generate(new FlagshipModel { IsFeatured = true }));
        }

        [Fact]
        public void FlagshipArchived()
        {
            var t = Compile("branching-flagship", typeof(FlagshipModel));
            AssertGolden("branching-flagship-archived", t.Generate(new FlagshipModel { IsFeatured = false, IsArchived = true }));
        }

        [Fact]
        public void FlagshipRegular()
        {
            var t = Compile("branching-flagship", typeof(FlagshipModel));
            AssertGolden("branching-flagship-regular", t.Generate(new FlagshipModel { IsFeatured = false, IsArchived = false }));
        }

        [Fact]
        public void InterleavedIsByteIdenticalToFlagshipWithTwoWarnings()
        {
            var t = Compile("branching-interleaved", typeof(FlagshipModel));
            var stripped = t.Context.CompileWarnings.Where(w => w.DiagnosticId == HeddleDiagnosticIds.BranchTextStripped).ToList();
            Assert.Equal(2, stripped.Count);

            AssertGolden("branching-flagship-featured", t.Generate(new FlagshipModel { IsFeatured = true }));
            AssertGolden("branching-flagship-archived", t.Generate(new FlagshipModel { IsFeatured = false, IsArchived = true }));
            AssertGolden("branching-flagship-regular", t.Generate(new FlagshipModel { IsFeatured = false, IsArchived = false }));
        }

        [Fact]
        public void ListAlternatingResetsPerIteration()
        {
            var t = Compile("branching-list-alternating", typeof(ListModel));
            var model = new ListModel
            {
                Items = new List<Item>
                {
                    new Item { IsFeatured = true },
                    new Item { IsFeatured = false, IsArchived = true },
                    new Item { IsFeatured = false, IsArchived = false },
                    new Item { IsFeatured = false, IsArchived = true },
                }
            };
            AssertGolden("branching-list-alternating", t.Generate(model));
        }

        public class OutModel { public bool A { get; set; } public bool B { get; set; } }
        public class PartialModel { public bool A { get; set; } public bool B { get; set; } }

        [Fact]
        public void OutProjectionSetPersistsAcrossOutCallerContentIndependent()
        {
            // def-body state persists across @out(); caller-content is independent.
            var t = Compile("branching-out-projection", typeof(OutModel));
            Assert.Equal("XCIN", t.Generate(new OutModel { A = true, B = true }));   // A satisfied -> X + caller CIN; def @else silent
            Assert.Equal("COUTZ", t.Generate(new OutModel { A = false, B = false })); // caller COUT + def @else Z
            AssertGolden("branching-out-projection", t.Generate(new OutModel { A = false, B = true }));
        }

        [Fact]
        public void PartialGetsFreshRootFrameParentSetBindsAcrossIt()
        {
            // @partial is non-branch: parent set binds across it; child renders under fresh root frame.
            HeddleTemplate.Configure(typeof(BranchingGoldenTests).GetTypeInfo().Assembly);
            BranchTestExtensions.Register();
            var options = new TemplateOptions("branching-partial-parent")
            {
                RootPath = "TestTemplate",
                FileNamePostfix = ".heddle"
            };
            var t = new HeddleTemplate(new CompileContext(options, typeof(PartialModel)));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("P-IFC-ELSE", t.Generate(new PartialModel { A = true, B = false }));
            AssertGolden("branching-partial", t.Generate(new PartialModel { A = false, B = true }));
        }

        [Fact]
        public void NestedSetsAreIndependent()
        {
            var t = Compile("branching-nested", typeof(NestedModel));
            Assert.Equal("IF", t.Generate(new NestedModel { Outer = true, InnerA = true }));
            Assert.Equal("IELSE", t.Generate(new NestedModel { Outer = true, InnerA = false }));
            Assert.Equal("OUTER-ELSE", t.Generate(new NestedModel { Outer = false }));
            AssertGolden("branching-nested", t.Generate(new NestedModel { Outer = true, InnerA = false }));
        }
    }
}
