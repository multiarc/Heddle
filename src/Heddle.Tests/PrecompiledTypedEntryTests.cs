using System;
using System.Reflection;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Typed entry points (P2-W2 exit criteria): a wrapper whose artifact fails validation
    /// throws <see cref="PrecompiledMismatchException"/>; with <see cref="PrecompiledTemplates.DefaultOptions"/>
    /// assigned, a late-bound function binds against its registry; two items in one assembly with
    /// different baked profiles render their own bytes while <c>TryResolve</c> keeps comparing the
    /// full triple. Serialized — DefaultOptions and the registry are process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class PrecompiledTypedEntryTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;
        private readonly TemplateOptions _savedDefaults;

        public PrecompiledTypedEntryTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            _savedDefaults = PrecompiledTemplates.DefaultOptions;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.DefaultOptions = _savedDefaults;
            PrecompiledTemplates.ResetForTests();
        }

        [Fact]
        public void FailedValidationThrowsMismatch()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("at-escape.heddle");
            Assembly assembly = RegisterSingle(row, TestCorpusIndex.CorpusDir);
            // The row baked row.Mode; DefaultOptions with a different mode fails the typed step.
            ExpressionMode other = row.Mode == ExpressionMode.Native
                ? ExpressionMode.MemberPathsOnly
                : ExpressionMode.Native;
            PrecompiledTemplates.DefaultOptions = new TemplateOptions { ExpressionMode = other };
            var ex = Assert.Throws<PrecompiledMismatchException>(() =>
                PrecompiledTemplates.BindTyped(assembly, row.Name, typeof(object)));
            Assert.Equal(PrecompiledFallbackReason.OptionsMismatch, ex.Reason);
        }

        [Fact]
        public void MissingKeyThrowsInvalidOperation()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("at-escape.heddle");
            Assembly assembly = RegisterSingle(row, TestCorpusIndex.CorpusDir);
            PrecompiledTemplates.DefaultOptions = new TemplateOptions();
            Assert.Throws<InvalidOperationException>(() =>
                PrecompiledTemplates.BindTyped(assembly, "no-such-key.heddle", typeof(object)));
        }

        [Fact]
        public void LateBoundFunctionBindsAgainstDefaultOptionsRegistry()
        {
            PrecompiledTemplates.ResetForTests();
            var row = Find("fn-standalone-late-bound.heddle");
            Assembly assembly = RegisterSingle(row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            CompiledFormHarness.ModelExFor(row, out modelType, out model);
            var defaults = new TemplateOptions();
            defaults.Functions = CompiledFormHarness.LateBoundFunctions();
            PrecompiledTemplates.DefaultOptions = defaults;
            var template = PrecompiledTemplates.BindTyped(assembly, row.Name, modelType);
            Assert.Equal("ADA\n", template.Generate(model));
        }

        [Fact]
        public void EachBakedProfileRendersItsOwnBytesWhileTryResolveComparesTheTriple()
        {
            PrecompiledTemplates.ResetForTests();
            // Static text is never encoded; expression output is: @(html(V)) double-encodes
            // under Html and single-encodes under Text, so the profiles genuinely differ.
            const string text = "@(html(V))";
            var model = new TypedEntryModel { V = "<b>x</b>" };
            Assembly assembly = RegisterProfiles(text, model);
            PrecompiledTemplates.DefaultOptions = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var html = PrecompiledTemplates.BindTyped(assembly, "fish-html.heddle", typeof(TypedEntryModel));
            var txt = PrecompiledTemplates.BindTyped(assembly, "fish-text.heddle", typeof(TypedEntryModel));
            Assert.Equal(DynamicRender(text, model, OutputProfile.Html), html.Generate(model));
            Assert.Equal(DynamicRender(text, model, OutputProfile.Text), txt.Generate(model));
            Assert.NotEqual(html.Generate(model), txt.Generate(model));

            var textOptions = new TemplateOptions { OutputProfile = OutputProfile.Text };
            PrecompiledTemplateInfo entry;
            Assert.True(PrecompiledTemplates.TryResolve("fish-text.heddle", textOptions, out entry));
            Assert.False(PrecompiledTemplates.TryResolve("fish-html.heddle", textOptions, out entry));
        }

        public class TypedEntryModel
        {
            public string V { get; set; }
        }

        private static CorpusIntentRow Find(string name)
        {
            foreach (var row in CorpusIntent.Rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                    return row;
            throw new InvalidOperationException("No intent row names '" + name + "'.");
        }

        private static Assembly RegisterSingle(CorpusIntentRow row, string rootPath)
        {
            string text = CompiledFormHarness.CorpusText(row.Name);
            var buildOptions = CompiledFormHarness.RowOptions(row.Name, row, rootPath);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, buildOptions, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + row.Name + ": " +
                CompiledFormHarness.Summarize(context) + ".");
            var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx,
                buildOptions, row);
            return CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(artifact),
                "HeddleTestAsm_Typed" + row.Name.Replace(".", "_"));
        }

        /// <summary>Two rows in one assembly with different baked profiles.</summary>
        private static Assembly RegisterProfiles(string text, TypedEntryModel model)
        {
            var parts = new CompiledArtifact[2];
            parts[0] = RecordInline(text, "fish-html.heddle", OutputProfile.Html, model);
            parts[1] = RecordInline(text, "fish-text.heddle", OutputProfile.Text, model);
            var merged = CompiledArtifactMerger.Merge(parts);
            return CompiledFormHarness.RegisterImage(CompiledFormWriter.Write(merged),
                "HeddleTestAsm_TypedProfiles");
        }

        private static CompiledArtifact RecordInline(string text, string key, OutputProfile profile,
            TypedEntryModel model)
        {
            var options = new TemplateOptions("fish")
            {
                RootPath = TestCorpusIndex.CorpusDir,
                FileNamePostfix = ".heddle",
                OutputProfile = profile,
                EnableFileChangeCheck = false,
                ExpressionMode = ExpressionMode.Native
            };
            var modelEx = new ExType(typeof(TypedEntryModel));
            var context = new CompileContext(options, modelEx);
            context.DeferUnboundFunctions = true;
            context.RecordForm = true;
            var template = new HeddleTemplate(text, context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + key + ": " +
                CompiledFormHarness.Summarize(context) + ".");
            return context.FormRecord.ToArtifact("test", "test", key, ContentHash.HashText(text),
                "reg-" + key, null, modelEx, false, false, profile.ToString(),
                ExpressionMode.Native.ToString(), options.TrimDirectiveLines);
        }

        private static string DynamicRender(string text, TypedEntryModel model, OutputProfile profile)
        {
            var options = new TemplateOptions("fish")
            {
                RootPath = TestCorpusIndex.CorpusDir,
                OutputProfile = profile,
                EnableFileChangeCheck = false,
                ExpressionMode = ExpressionMode.Native
            };
            var template = new HeddleTemplate(text, new CompileContext(options,
                new ExType(typeof(TypedEntryModel))));
            Assert.True(template.CompileResult.Success, "Dynamic reference failed under " + profile + ".");
            return template.Generate(model);
        }
    }
}
