using System;
using System.Buffers;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using System.Text;
using Heddle.Data;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Shared build/record/register/materialize/compare pipeline for the compiled-form parity
    /// suite (P1-W9). Every test class in this suite runs the same steps the engine's own dynamic tier
    /// runs, then byte-compares: text compile with form recording and unbound-function deferral, artifact
    /// write/read round-trip, marker-assembly registration, gauntlet validation, request-scoped
    /// materialization, and a three-sink render (string, writer, UTF-8) against a dynamic reference.</summary>
    internal static class CompiledFormHarness
    {
        internal sealed class RowResult
        {
            internal CompiledArtifact Artifact;
            internal byte[] Image;
            internal PrecompiledTemplateInfo Entry;
            internal IProcessStrategy Strategy;
        }

        internal static string CompatibleVersion
        {
            get
            {
                var v = typeof(PrecompiledTemplates).Assembly.GetName().Version ?? new Version(1, 0, 0, 0);
                return v.Major + "." + Math.Max(v.Minor, 0) + "." + Math.Max(v.Build, 0);
            }
        }

        internal static string Decode(byte[] raw)
        {
            using (var stream = new MemoryStream(raw, false))
            using (var reader = new StreamReader(stream, new UTF8Encoding(false), true))
                return reader.ReadToEnd();
        }

        internal static string CorpusText(string name) => Decode(TestCorpusIndex.Bytes(name));

        internal static TemplateOptions RowOptions(string name, CorpusIntentRow row, string rootPath)
        {
            var options = new TemplateOptions(Path.GetFileNameWithoutExtension(name))
            {
                RootPath = rootPath,
                FileNamePostfix = ".heddle",
                OutputProfile = OutputProfile.Text,
                EnableFileChangeCheck = false
            };
            options.ExpressionMode = row.Mode;
            return options;
        }

        internal static ExType ModelExFor(CorpusIntentRow row, out Type modelType, out object model)
        {
            modelType = null;
            model = null;
            if (row.Render == CorpusRender.WithModel)
            {
                modelType = CorpusModels.For(row.Name);
                model = NewModel(modelType);
            }
            if (IsLateBoundRow(row.Name))
            {
                // Overload ranking needs a static start type on both tiers alike.
                model = new { Name = "Ada" };
                modelType = model.GetType();
            }
            return modelType == null ? ExType.Dynamic : new ExType(modelType);
        }

        internal static bool IsLateBoundRow(string name) =>
            string.Equals(name, "fn-standalone-late-bound.heddle", StringComparison.Ordinal) ||
            string.Equals(name, "fn-typed-consumer-late-bound.heddle", StringComparison.Ordinal);

        internal static bool IsUnresolvableRow(string name) =>
            string.Equals(name, "fn-late-bound.heddle", StringComparison.Ordinal) ||
            string.Equals(name, "fn-unresolvable-marker.heddle", StringComparison.Ordinal);

        internal static object NewModel(Type modelType)
        {
            if (modelType == null)
                return new ExpandoObject();
            try
            {
                return Activator.CreateInstance(modelType);
            }
            catch (MissingMethodException)
            {
#pragma warning disable SYSLIB0050 // Parity needs an instance, not a serialized one.
                return FormatterServices.GetUninitializedObject(modelType);
#pragma warning restore SYSLIB0050
            }
        }

        internal static Heddle.Runtime.Expressions.FunctionRegistry LateBoundFunctions()
        {
            var registry = new Heddle.Runtime.Expressions.FunctionRegistry();
            registry.Register("toUpper", (Func<string, string>)(s => s == null ? null : s.ToUpperInvariant()));
            return registry;
        }

        /// <summary>Text-compiles with recording and deferral armed. Returns the context for error
        /// inspection; the template carries the compile result.</summary>
        internal static HeddleTemplate BuildRecording(string text, TemplateOptions options, ExType modelEx,
            out CompileContext context)
        {
            context = new CompileContext(options, modelEx);
            context.DeferUnboundFunctions = true;
            context.RecordForm = true;
            return new HeddleTemplate(text, context);
        }

        internal static CompiledArtifact ToArtifact(CompileContext context, string name, string text,
            ExType modelEx, TemplateOptions options, CorpusIntentRow row)
        {
            return context.FormRecord.ToArtifact("test", "test", name, ContentHash.HashText(text),
                "reg-" + name, null, modelEx, false, false, "Text", row.Mode.ToString(),
                options.TrimDirectiveLines);
        }

        internal static void AssertRefusalsMatch(CorpusIntentRow row, CompiledArtifact artifact)
        {
            var declared = new SortedSet<string>(
                row.Refusals.Select(r => r.ToString()), StringComparer.Ordinal);
            var observed = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var template in artifact.Templates)
                foreach (var site in template.RefusalSites)
                    observed.Add(site.Class.ToString());
            Assert.True(declared.SetEquals(observed),
                "Refusal sites [" + string.Join(",", observed) + "] != declared [" +
                string.Join(",", declared) + "] for " + row.Name + ".");
        }

        internal sealed class HarnessMarker : IHeddleCompiledArtifact
        {
            internal static byte[] Image;

            public Stream OpenArtifact() => new MemoryStream(Image ?? new byte[0], false);
        }

        /// <summary>Registers artifact bytes through a dynamic marker assembly, the way the generator's
        /// output registers: a [HeddleCompiledTemplates] marker naming an artifact opener.</summary>
        internal static Assembly RegisterImage(byte[] image, string assemblyName)
        {
            HarnessMarker.Image = image;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(assemblyName + "_" + Guid.NewGuid().ToString("N")),
                AssemblyBuilderAccess.Run);
            var ctor = typeof(HeddleCompiledTemplatesAttribute).GetConstructor(
                new[] { typeof(Type), typeof(int), typeof(string) });
            assembly.SetCustomAttribute(new CustomAttributeBuilder(ctor, new object[]
            {
                typeof(HarnessMarker), PrecompiledSchema.CompiledFormSchemaVersion, CompatibleVersion
            }));
            PrecompiledTemplates.Register(assembly);
            return assembly;
        }

        internal static TemplateOptions RequestOptions(CorpusIntentRow row, string rootPath)
        {
            var options = RowOptions(row.Name, row, rootPath);
            if (IsLateBoundRow(row.Name))
                options.Functions = LateBoundFunctions();
            return options;
        }

        /// <summary>Full per-row pipeline through registration. Asserts at the first divergent step.</summary>
        internal static RowResult RegisterRow(CorpusIntentRow row, string rootPath)
        {
            string text = CorpusText(row.Name);
            var buildOptions = RowOptions(row.Name, row, rootPath);
            Type modelType;
            object model;
            var modelEx = ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = BuildRecording(text, buildOptions, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build recorded errors for " + row.Name + ": " + Summarize(context) + ".");
            var artifact = ToArtifact(context, row.Name, text, modelEx, buildOptions, row);
            AssertRefusalsMatch(row, artifact);
            var image = CompiledFormWriter.Write(artifact);
            var back = CompiledFormReader.Read(image);
            AssertRefusalsMatch(row, back);
            RegisterImage(image, "HeddleTestAsm_Parity" + row.Name.Replace(".", "_"));
            var requestOptions = RequestOptions(row, rootPath);
            var report = PrecompiledTemplates.ValidateAll(requestOptions);
            Assert.True(report.Failures.Count == 0, "Gate refused " + row.Name + ": " +
                GateDetail(report) + ".");
            PrecompiledTemplateInfo entry;
            Assert.True(PrecompiledTemplates.TryResolve(row.Name, requestOptions, out entry) && entry != null,
                "TryResolve refused " + row.Name + ".");
            var strategy = entry.GetStrategy(requestOptions);
            if (strategy == null)
            {
                PrecompiledFallbackReason reason;
                string detail;
                string fault = entry.TryGetRequestFault(requestOptions, out reason, out detail) ?
                    reason + ": " + detail : "<no fault recorded>";
                Assert.True(false, "Materialization fault for " + row.Name + ": " + fault + ".");
            }
            return new RowResult { Artifact = back, Image = image, Entry = entry, Strategy = strategy };
        }

        /// <summary>Formats the first gate failure without indexing an empty list: the
        /// failure message of an assert is evaluated eagerly, so Failures[0] throws even
        /// when the gate passes.</summary>
        internal static string GateDetail(PrecompiledValidationReport report)
        {
            if (report == null || report.Failures.Count == 0)
                return "<no failure recorded>";
            var first = report.Failures[0];
            return first.Reason + ": " + first.Detail;
        }

        internal static string Summarize(CompileContext context)
        {
            var parts = new List<string>();
            foreach (var error in context.CompileErrors)
                parts.Add(error.DiagnosticId + ":" + error.Error);
            return parts.Count == 0 ? "<no detail>" : string.Join("; ", parts);
        }

        /// <summary>Renders both tiers through all three sinks and asserts byte identity, returning the
        /// shared output. The dynamic reference compiles without recording or deferral.</summary>
        internal static string AssertThreeSinkParity(CorpusIntentRow row, IProcessStrategy strategy,
            string rootPath)
        {
            Type modelType;
            object model;
            var modelEx = ModelExFor(row, out modelType, out model);
            string text = CorpusText(row.Name);

            string s1 = PrecompiledRuntime.GenerateString(strategy, model, null, null);
            string s2;
            using (var writer = new StringWriter())
            {
                PrecompiledRuntime.GenerateToWriter(strategy, model, null, null, writer);
                s2 = writer.ToString();
            }
            var seq = new ArrayBufferWriter<byte>();
            PrecompiledRuntime.GenerateUtf8(strategy, model, null, null, seq);
            string s3 = Encoding.UTF8.GetString(seq.WrittenSpan.ToArray());

            var renderOptions = RequestOptions(row, rootPath);
            var dynamic = new HeddleTemplate(text, new CompileContext(renderOptions, modelEx));
            Assert.True(dynamic.CompileResult.Success, "Dynamic reference failed for " + row.Name + ".");
            string d1 = dynamic.Generate(model);
            string d2;
            using (var writer = new StringWriter())
            {
                dynamic.Generate(model, writer);
                d2 = writer.ToString();
            }
            var dseq = new ArrayBufferWriter<byte>();
            dynamic.Generate(model, dseq);
            string d3 = Encoding.UTF8.GetString(dseq.WrittenSpan.ToArray());

            Assert.True(s1 == d1 && s2 == d2 && s3 == d3,
                "Byte divergence for " + row.Name + ": string=" + (s1 == d1) +
                " writer=" + (s2 == d2) + " utf8=" + (s3 == d3) + ".");
            return s1;
        }
    }
}
