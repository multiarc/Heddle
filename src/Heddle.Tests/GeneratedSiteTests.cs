using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Heddle.Language.Members;
using Heddle.Precompiled;
using Heddle.Runtime.Parameters;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Generated site coverage (P3-R3 through P3-R5, probed through P3-R6 strict mode): for each
    /// corpus <c>Compiles</c> row with the table on, every accessor and native expression is served from
    /// the table (probe: strict mode does not throw) and the rendered bytes equal the dynamic tier's.
    /// The hop-form parity fact pins the <c>NullDefaultConditional</c> semantics the printer must
    /// reproduce, against the engine's own <c>Expression.Default(propertyType)</c> shape, over a null
    /// receiver. Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class GeneratedSiteTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public GeneratedSiteTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        public static IEnumerable<object[]> TableCoveredRows()
        {
            // Compiles rows with no refusal, late-bound or decline baggage: strict mode must serve every
            // one of their sites from the table and throw for none of them.
            foreach (var row in CorpusIntent.Rows)
                if (row.Tier == CorpusTier.Compiles && row.LateBound.Count == 0 &&
                    row.PrinterDeclines.Count == 0 &&
                    !CompiledFormHarness.IsUnresolvableRow(row.Name))
                    yield return new object[] { row.Name };
        }

        private static CorpusIntentRow Find(string name)
        {
            foreach (var row in CorpusIntent.Rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                    return row;
            throw new InvalidOperationException("No intent row names '" + name + "'.");
        }

        [Theory]
        [MemberData(nameof(TableCoveredRows))]
        public void TableOnServesEverySite(string name)
        {
            if (!CompiledFormHarness.StrictModeSupported)
                Assert.Skip("The table-on probe needs the P3-A engine slice (site table + strict load).");
            var row = Find(name);
            PrecompiledTemplates.ResetForTests();
            using (var guard = new FallbackGuard())
            {
                var result = CompiledFormHarness.RegisterRowWithSites(
                    row, TestCorpusIndex.CorpusDir, true);
                if (row.Render != CorpusRender.ResolveOnly)
                    CompiledFormHarness.AssertThreeSinkParity(row, result.Strategy, TestCorpusIndex.CorpusDir);
                guard.AssertQuiet();
            }
            AssertStrictDoesNotThrow(row);
            PrecompiledTemplates.ResetForTests();
        }

        private static void AssertStrictDoesNotThrow(CorpusIntentRow row)
        {
            // The probe: with every site printed, strict load has nothing left to compile.
            AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", true);
            try
            {
                PrecompiledTemplates.ResetForTests();
                string text = CompiledFormHarness.CorpusText(row.Name);
                var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
                var modelEx = CompiledFormHarness.ModelExFor(row, out _, out _);
                var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out var context);
                Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                    "Build recorded errors for " + row.Name + ".");
                var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
                var image = Precompiled.CompiledForm.CompiledFormWriter.Write(artifact);
                CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_Site" + row.Name.Replace(".", "_"));
                var requestOptions = CompiledFormHarness.RequestOptions(row, TestCorpusIndex.CorpusDir);
                var strictProperty = typeof(Heddle.Data.TemplateOptions).GetProperty("PrecompiledStrictLoad");
                if (strictProperty != null)
                    strictProperty.SetValue(requestOptions, true);
                PrecompiledTemplateInfo entry;
                Assert.True(PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) && entry != null,
                    "TryResolve refused " + row.Name + " with the table on.");
                var strategy = entry.GetStrategy(requestOptions);
                Assert.True(strategy != null, "Strict load threw for " + row.Name + ": a site was not served.");
            }
            finally
            {
                AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", false);
            }
        }

        // The P3-R3 printed shape under test, spelled exactly as the MemberAccessorPrinter must spell it:
        // the engine's start cast, one local per hop, the HopForm the engine chose per hop, one box.
        public sealed class AccessorOrder
        {
            public AccessorCustomer Customer { get; set; }
        }

        public sealed class AccessorCustomer
        {
            public AccessorAddress Address { get; set; }
        }

        public sealed class AccessorAddress
        {
            public decimal Total { get; set; }
            public string City { get; set; }
        }

        private static object PrintedValueAccessor(object model)
        {
            var v0 = (AccessorOrder)model;                            // the engine's start cast
            var v1 = v0.Customer;                                     // Direct
            var v2 = v1 == null ? null : v1.Address;                  // NullConditional
            var v3 = v2 == null ? default(decimal) : v2.Total;        // NullDefaultConditional
            return (object)v3;                                        // boxed exactly once
        }

        private static object PrintedReferenceAccessor(object model)
        {
            var v0 = (AccessorOrder)model;
            var v1 = v0.Customer;                                     // Direct
            var v2 = v1 == null ? null : v1.Address;                  // NullConditional
            return v2 == null ? null : (object)v2.City;               // NullConditional: null propagates
        }

        private static Func<object, object> EngineAccessor(string finalProperty)
        {
            var chain = new List<(Type, PropertyInfo)>
            {
                (typeof(AccessorOrder), typeof(AccessorOrder).GetProperty("Customer")),
                (typeof(AccessorCustomer), typeof(AccessorCustomer).GetProperty("Address")),
                (typeof(AccessorAddress), typeof(AccessorAddress).GetProperty(finalProperty)),
            };
            return ModelParameter.GetPropertyChainAccessor(chain).Compile();
        }

        [Fact]
        public void NullDefaultConditionalMatchesEngineDefaultOverNullReceiver()
        {
            // The engine chose NullDefaultConditional for a non-nullable value hop off a reference
            // receiver; over a null receiver both spellings yield the boxed default(TValue).
            Assert.Equal(HopForm.NullDefaultConditional,
                MemberHopRule.Form(false, true));
            var engine = EngineAccessor("Total");
            var empty = new AccessorOrder();
            Assert.Equal(engine(empty), PrintedValueAccessor(empty));
            Assert.Equal((object)default(decimal), PrintedValueAccessor(empty));
            var full = new AccessorOrder
            {
                Customer = new AccessorCustomer { Address = new AccessorAddress { Total = 129.50m } }
            };
            Assert.Equal(engine(full), PrintedValueAccessor(full));
            Assert.Equal((object)129.50m, PrintedValueAccessor(full));
        }

        [Fact]
        public void NullConditionalPropagatesNullLikeEngineChain()
        {
            Assert.Equal(HopForm.NullConditional, MemberHopRule.Form(false, false));
            var engine = EngineAccessor("City");
            var empty = new AccessorOrder();
            Assert.Equal(engine(empty), PrintedReferenceAccessor(empty));
            Assert.Null(PrintedReferenceAccessor(empty));
            var full = new AccessorOrder
            {
                Customer = new AccessorCustomer { Address = new AccessorAddress { City = "Minsk" } }
            };
            Assert.Equal(engine(full), PrintedReferenceAccessor(full));
            Assert.Equal((object)"Minsk", PrintedReferenceAccessor(full));
        }

        [Fact]
        public void DirectHopReadsValueTypeReceiver()
        {
            Assert.Equal(HopForm.Direct, MemberHopRule.Form(true, true));
            Assert.Equal(HopForm.Direct, MemberHopRule.Form(true, false));
        }

        // P3-R5's consumer-assembly claim is covered by the AOT sample's assemblies.txt gate (a host
        // rendering only printed C# templates loads no Microsoft.CodeAnalysis type). An in-test version
        // cannot assert the same fact: harness artifacts are data-only by construction, so their C# sites
        // always compile through Roslyn at load. A printer-driven in-test version returns with the P3-W2
        // C# printer.
    }
}
