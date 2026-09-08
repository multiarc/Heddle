using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Strict no-load-time-compilation mode (P3-R6) and its corpus set equality (P3-R7): the set
    /// of rows that throw <c>PrecompiledStrictLoadException</c> under strict load equals, by set equality,
    /// the rows whose <c>refusals</c> are non-empty, the rows declaring <c>lateBound</c>, and the rows
    /// declaring <c>printerDeclines</c>. The corpus is expected to declare no printer declines, and a
    /// decline the printer records on a row without the field is a red gate.
    /// Serialized — the registry and the strict AppContext switch are process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class StrictLoadTests : IDisposable
    {
        private const string StrictSwitch = "Heddle.Precompiled.StrictLoad";
        private const string StrictExceptionName = "Heddle.Precompiled.PrecompiledStrictLoadException";

        private readonly Action<PrecompiledFallbackEvent> _savedCallback;
        private readonly TemplateOptions _savedDefaultOptions;

        public StrictLoadTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            _savedDefaultOptions = PrecompiledTemplates.DefaultOptions;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            AppContext.SetSwitch(StrictSwitch, false);
            PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        private static CorpusIntentRow Find(string name)
        {
            foreach (var row in CorpusIntent.Rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                    return row;
            throw new InvalidOperationException("No intent row names '" + name + "'.");
        }

        /// <summary>The rows P3-R7 expects to throw under strict load, derived from declarations alone.</summary>
        internal static IReadOnlyList<string> ExpectedStrictRows() =>
            CorpusIntent.Rows
                .Where(r => r.Bound &&
                    (r.Refusals.Count != 0 || r.LateBound.Count != 0 || r.PrinterDeclines.Count != 0))
                .Select(r => r.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

        private static void RequireStrictMode(string fact)
        {
            if (!CompiledFormHarness.StrictModeSupported)
                Assert.Skip(fact + " needs the P3-A engine slice (PrecompiledStrictLoadException + TemplateOptions.PrecompiledStrictLoad).");
        }

        private static bool IsStrictException(Exception ex) =>
            ex != null && string.Equals(ex.GetType().FullName, StrictExceptionName, StringComparison.Ordinal);

        private static string StrictSiteKind(Exception ex) =>
            (string)ex.GetType().GetProperty("SiteKind").GetValue(ex);

        private static int StrictSiteOrdinal(Exception ex) =>
            (int)ex.GetType().GetProperty("SiteOrdinal").GetValue(ex);

        private static string StrictTemplateKey(Exception ex) =>
            (string)ex.GetType().GetProperty("TemplateKey").GetValue(ex);

        /// <summary>Strict request options: the AppContext switch seeds the option (the spec'd default),
        /// and the property is pinned directly when the engine exposes it.</summary>
        private static TemplateOptions StrictRequestOptions(CorpusIntentRow row, string rootPath)
        {
            AppContext.SetSwitch(StrictSwitch, true);
            var options = CompiledFormHarness.RequestOptions(row, rootPath);
            var property = typeof(TemplateOptions).GetProperty("PrecompiledStrictLoad");
            if (property != null)
                property.SetValue(options, true);
            return options;
        }

        /// <summary>Materializes one row under strict load. Returns true with the strict site's kind and
        /// ordinal when the strict exception fires; false when the row materializes.</summary>
        private static bool TryStrictMaterialize(CorpusIntentRow row, out string siteKind, out int siteOrdinal)
        {
            siteKind = null;
            siteOrdinal = -1;
            PrecompiledTemplates.ResetForTests();
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            var modelEx = CompiledFormHarness.ModelExFor(row, out _, out _);
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out var context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + row.Name + ": " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            var image = Precompiled.CompiledForm.CompiledFormWriter.Write(artifact);
            CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_Strict" + row.Name.Replace(".", "_"));
            var requestOptions = StrictRequestOptions(row, TestCorpusIndex.CorpusDir);
            try
            {
                PrecompiledTemplateInfo entry;
                if (!PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) || entry == null)
                    return false;
                entry.GetStrategy(requestOptions);
                return false;
            }
            catch (Exception ex) when (IsStrictException(ex))
            {
                siteKind = StrictSiteKind(ex);
                siteOrdinal = StrictSiteOrdinal(ex);
                Assert.Equal(row.Name, StrictTemplateKey(ex));
                return true;
            }
            finally
            {
                AppContext.SetSwitch(StrictSwitch, false);
            }
        }

        [Fact]
        public void NoRowDeclaresPrinterDeclines()
        {
            // The corpus is expected to declare none of the last (P3-R7): a decline the printer
            // records on a row without the field is a red gate, so the declared set starts empty.
            var declared = CorpusIntent.Rows.Where(r => r.PrinterDeclines.Count != 0).Select(r => r.Name).ToList();
            Assert.True(declared.Count == 0,
                "Rows declaring printerDeclines: " + string.Join(", ", declared) + ".");
        }

        [Fact]
        public void LateBoundDeclarationsNameFunctionsOnBoundRows()
        {
            foreach (var row in CorpusIntent.Rows)
            {
                if (row.LateBound.Count == 0)
                    continue;
                Assert.True(row.Bound, row.Name + " declares lateBound but produces no artifact.");
                foreach (var name in row.LateBound)
                    Assert.False(string.IsNullOrEmpty(name), row.Name + " declares an empty late-bound name.");
            }
        }

        [Fact]
        public void StrictSetEqualsDeclaredSet()
        {
            RequireStrictMode(nameof(StrictSetEqualsDeclaredSet));
            var expected = new SortedSet<string>(ExpectedStrictRows(), StringComparer.Ordinal);
            var observed = new SortedSet<string>(StringComparer.Ordinal);
            var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in CorpusIntent.Rows)
            {
                if (!row.Bound || CompiledFormHarness.IsUnresolvableRow(row.Name))
                    continue;
                if (TryStrictMaterialize(row, out var kind, out _))
                {
                    observed.Add(row.Name);
                    kinds[row.Name] = kind;
                }
            }
            Assert.True(expected.SetEquals(observed),
                "Strict-throwing rows [" + string.Join(",", observed) + "] != declared [" +
                string.Join(",", expected) + "]. Kinds: " +
                string.Join("; ", kinds.Select(kv => kv.Key + "=" + kv.Value)) + ".");
        }

        [Fact]
        public void RefusalSiteThrowsNamingOrdinalAndKind()
        {
            RequireStrictMode(nameof(RefusalSiteThrowsNamingOrdinalAndKind));
            var row = Find("ext-site-fallback.heddle");
            Assert.True(TryStrictMaterialize(row, out var kind, out var ordinal),
                "The refusal row should throw under strict load.");
            Assert.Equal("RefusalSite", kind);
            Assert.True(ordinal >= 0, "The strict exception should name the refusal site ordinal.");
        }

        [Fact]
        public void LateBoundFunctionThrowsLateBoundKind()
        {
            RequireStrictMode(nameof(LateBoundFunctionThrowsLateBoundKind));
            foreach (var name in new[] { "fn-standalone-late-bound.heddle", "fn-typed-consumer-late-bound.heddle" })
            {
                var row = Find(name);
                Assert.True(TryStrictMaterialize(row, out var kind, out _),
                    name + " should throw under strict load.");
                Assert.Equal("LateBound", kind);
            }
        }

        [Fact]
        public void DynamicAndModelLessTemplatesRenderUnderStrict()
        {
            // Declared classes, permitted under strict mode: DLR call-site creation for a DynamicHop
            // (:: dynamic and model-less templates) is not compilation, so these render.
            RequireStrictMode(nameof(DynamicAndModelLessTemplatesRenderUnderStrict));
            foreach (var name in new[] { "at-escape.heddle", "branching-out-projection.heddle" })
            {
                var row = Find(name);
                PrecompiledTemplates.ResetForTests();
                var result = RegisterStrictRow(row);
                CompiledFormHarness.AssertThreeSinkParity(row, result.Strategy, TestCorpusIndex.CorpusDir);
                PrecompiledTemplates.ResetForTests();
            }
        }

        private static CompiledFormHarness.RowResult RegisterStrictRow(CorpusIntentRow row)
        {
            PrecompiledTemplates.ResetForTests();
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            var modelEx = CompiledFormHarness.ModelExFor(row, out _, out _);
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out var context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + row.Name + ": " + CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            var image = Precompiled.CompiledForm.CompiledFormWriter.Write(artifact);
            CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_StrictRender" + row.Name.Replace(".", "_"));
            var requestOptions = StrictRequestOptions(row, TestCorpusIndex.CorpusDir);
            try
            {
                PrecompiledTemplateInfo entry;
                Assert.True(PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) && entry != null,
                    "TryResolve refused " + row.Name + " under strict load.");
                var strategy = entry.GetStrategy(requestOptions);
                Assert.True(strategy != null, "Strict materialization fault for " + row.Name + ".");
                return new CompiledFormHarness.RowResult
                {
                    Artifact = artifact, Image = image, Entry = entry, Strategy = strategy
                };
            }
            finally
            {
                AppContext.SetSwitch(StrictSwitch, false);
            }
        }

        [Fact]
        public void TypedWrapperThrowsFromBindTypedUnderStrict()
        {
            RequireStrictMode(nameof(TypedWrapperThrowsFromBindTypedUnderStrict));
            var row = Find("ext-site-fallback.heddle");
            PrecompiledTemplates.ResetForTests();
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            var modelEx = CompiledFormHarness.ModelExFor(row, out var modelType, out _);
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out var context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + row.Name + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            var image = Precompiled.CompiledForm.CompiledFormWriter.Write(artifact);
            var assembly = CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_StrictBindTyped");
            AppContext.SetSwitch(StrictSwitch, true);
            PrecompiledTemplates.DefaultOptions = new TemplateOptions("strict-probe");
            try
            {
                var ex = Assert.ThrowsAny<Exception>(() =>
                    PrecompiledTemplates.BindTyped(assembly, row.Name, modelType ?? typeof(object)));
                Assert.True(IsStrictException(ex),
                    "BindTyped under strict load should throw the strict exception, threw " +
                    ex.GetType().FullName + ".");
            }
            finally
            {
                AppContext.SetSwitch(StrictSwitch, false);
                PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            }
        }
    }
}
