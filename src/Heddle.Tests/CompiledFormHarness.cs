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
    /// suite. Every test class in this suite runs the same steps the engine's own dynamic tier
    /// runs, then byte-compares: text compile with form recording and unbound-function deferral, artifact
    /// write/read round-trip, marker-assembly registration, gauntlet validation, request-scoped
    /// materialization, and a three-sink render (string, writer, UTF-8) against a dynamic reference.</summary>
    internal static class CompiledFormHarness
    {
        internal sealed class RowResult
        {
            /// <summary>Which of the three P1-R9 passes produced this result: <see cref="InMemoryPass"/>,
            /// <see cref="FileBackedPass"/> or <see cref="StagedPass"/>.</summary>
            internal string Pass;
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

        /// <summary>The three passes of P1-R9: the artifact loaded from memory; written under
        /// <c>TestOutput/</c> and loaded from the file's bytes; and the entry staged on disk in its real
        /// encoding with the gauntlet's staleness step switched on.</summary>
        internal const string InMemoryPass = "in-memory";
        internal const string FileBackedPass = "file-backed";
        internal const string StagedPass = "staged";
        internal static readonly string[] Passes = { InMemoryPass, FileBackedPass, StagedPass };

        private static readonly List<KeyValuePair<string, PrecompiledRefusalClass>> ExpectedRefusals =
            new List<KeyValuePair<string, PrecompiledRefusalClass>>();

        /// <summary>Declares that the artifact for <paramref name="key"/> carries one refusal site of
        /// <paramref name="refusalClass"/> beyond what its intent row declares. <see cref="AssertRefusalsMatch"/>
        /// fails if the site is not there; the registration pass consumes the declaration, and one left
        /// unconsumed is a test defect, reported by <see cref="AssertNoPendingRefusals"/>.</summary>
        internal static void ExpectRefusal(string key, PrecompiledRefusalClass refusalClass)
        {
            ExpectedRefusals.Add(new KeyValuePair<string, PrecompiledRefusalClass>(key, refusalClass));
        }

        internal static void AssertNoPendingRefusals()
        {
            if (ExpectedRefusals.Count == 0)
                return;
            var pending = string.Join(", ", ExpectedRefusals.Select(p => p.Key + " " + p.Value));
            ExpectedRefusals.Clear();
            Assert.Fail("Declared refusals never matched an artifact: " + pending + ".");
        }

        internal static void AssertRefusalsMatch(CorpusIntentRow row, CompiledArtifact artifact)
        {
            var declared = new SortedSet<string>(
                row.Refusals.Select(r => r.ToString()), StringComparer.Ordinal);
            foreach (var expected in ExpectedRefusals)
                if (string.Equals(expected.Key, row.Name, StringComparison.Ordinal))
                    declared.Add(expected.Value.ToString());
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

        /// <summary>Builds the smallest artifact a loader row needs: a header plus caller-supplied
        /// template/extension/function rows. For gauntlet/registry unit tests that need rows without
        /// compiling a template.</summary>
        internal static CompiledArtifact MinimalArtifact()
        {
            var artifact = new CompiledArtifact
            {
                Header = new CompiledHeader
                {
                    EngineVersion = CompatibleVersion,
                    BuilderVersion = "test",
                    ExpressionMode = "Native",
                    TrimDirectiveLines = false,
                    DefaultOutputProfile = "Text"
                }
            };
            // Template rows point at document 0 by default; the writer requires the target to exist
            // even though the gauntlet and the registry never read it.
            artifact.Documents.Add(new CompiledDocument
            {
                ParseFacts = new CompiledParseFacts
                {
                    Offset = 0,
                    VisibleDefinitionRefs = new List<int>()
                }
            });
            return artifact;
        }

        internal static CompiledTemplateRow TemplateRow(string key,
            string profile = "Text", string mode = "Native", bool trim = false,
            string registeredName = null, string contentHash = "0",
            CompiledTypeRef modelType = null, bool ambient = false,
            IList<int> extensionRefs = null, IList<int> functionRefs = null) =>
            new CompiledTemplateRow
            {
                Key = key,
                RegisteredName = registeredName,
                ContentHash = contentHash,
                ModelType = modelType,
                ModelTypeIsAmbient = ambient,
                Options = new CompiledOptionsFingerprint { Profile = profile, Mode = mode, Trim = trim },
                ExtensionRefs = extensionRefs ?? new List<int>(),
                FunctionRefs = functionRefs ?? new List<int>()
            };

        internal static NamedTypeRef TypeRef(Type type) =>
            new NamedTypeRef(type.FullName, type.Assembly.GetName().Name, false);

        internal static NamedTypeRef TypeRef(string fullName, string assemblySimpleName) =>
            new NamedTypeRef(fullName, assemblySimpleName, false);

        /// <summary>Round-trips an artifact through bytes and returns the loader row — the internal-constructor
        /// path every post-generator test uses instead of the deleted public constructors.</summary>
        internal static PrecompiledTemplateInfo LoaderRow(CompiledArtifact artifact, int rowIndex = 0)
        {
            var image = CompiledFormWriter.Write(artifact);
            var back = CompiledFormReader.Read(image);
            return new PrecompiledTemplateInfo(typeof(CompiledFormHarness).Assembly,
                new LoadedArtifact(back), rowIndex, null);
        }

        /// <summary>Registers an artifact's rows through a dynamic marker assembly and returns it, for the
        /// registry suites. Each call mints a fresh assembly; bytes are consumed synchronously at register
        /// time, so sequential registrations with different rows are independent.</summary>
        internal static Assembly RegisterArtifact(CompiledArtifact artifact, string assemblyName) =>
            RegisterImage(CompiledFormWriter.Write(artifact), assemblyName);

        /// <summary>Registers artifact bytes through a dynamic marker assembly, the way the build's
        /// output registers: a [HeddleCompiledTemplates] marker naming an artifact opener.</summary>
        internal static Assembly RegisterImage(byte[] image, string assemblyName, bool exactName = false)
        {
            HarnessMarker.Image = image;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName(exactName ? assemblyName : assemblyName + "_" + Guid.NewGuid().ToString("N")),
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

        /// <summary>The public switch the P3-R7 evidence runs pivot on: the loader prefers a generated
        /// site unless this is <c>false</c> (default <c>true</c>). Set through the public surface so the
        /// "without" arm exercises the data path exactly as a host would.</summary>
        internal const string UseGeneratedSitesSwitch = "Heddle.Precompiled.UseGeneratedSites";

        /// <summary>Per-row pipeline with the site-table preference pinned to one position (P3-R7: every
        /// corpus row runs twice, switch on and off). Restores the default-<c>true</c> position after.</summary>
        internal static RowResult RegisterRowWithSites(CorpusIntentRow row, string rootPath, bool useGeneratedSites)
        {
            AppContext.SetSwitch(UseGeneratedSitesSwitch, useGeneratedSites);
            try
            {
                return RegisterRow(row, rootPath);
            }
            finally
            {
                AppContext.SetSwitch(UseGeneratedSitesSwitch, true);
            }
        }

        /// <summary>Every P1-R9 pass for one row, each in a fresh registry: in-memory, file-backed and
        /// staged, with the site-table preference pinned as <paramref name="useGeneratedSites"/> says.</summary>
        internal static List<RowResult> RegisterRowPasses(CorpusIntentRow row, string rootPath,
            bool useGeneratedSites)
        {
            var results = new List<RowResult>(Passes.Length);
            foreach (var pass in Passes)
            {
                PrecompiledTemplates.ResetForTests();
                AppContext.SetSwitch(UseGeneratedSitesSwitch, useGeneratedSites);
                try
                {
                    results.Add(RegisterRow(row, rootPath, pass));
                }
                finally
                {
                    AppContext.SetSwitch(UseGeneratedSitesSwitch, true);
                }
            }
            return results;
        }

        /// <summary>Full per-row pipeline through registration, in-memory pass. Asserts at the first
        /// divergent step.</summary>
        internal static RowResult RegisterRow(CorpusIntentRow row, string rootPath) =>
            RegisterRow(row, rootPath, InMemoryPass);

        private static string SafeName(string name) =>
            name.Replace('.', '_').Replace('/', '_').Replace('\\', '_');

        private static readonly object StagingGate = new object();
        private static string _stagedRoot;

        /// <summary>The corpus staged once per process under <c>TestOutput/staged-corpus/</c>, every file
        /// in its real on-disk encoding (a lone-row artifact carries no import rows, so materialization
        /// reads imports off the request root exactly as a deployed host with template files beside its
        /// assembly does). The staged pass points its request root here and turns the staleness step on,
        /// so the gauntlet hashes the entry and its recorded imports from disk.</summary>
        private static string StageEntry(CorpusIntentRow row, CompiledArtifact artifact)
        {
            lock (StagingGate)
            {
                if (_stagedRoot == null)
                {
                    string root = Path.Combine(AppContext.BaseDirectory, TestCorpusIndex.WrittenArtifactFolder,
                        "staged-corpus");
                    if (Directory.Exists(root))
                        Directory.Delete(root, true);
                    foreach (var source in Directory.GetFiles(TestCorpusIndex.CorpusDir, "*", SearchOption.AllDirectories))
                    {
                        string relative = source.Substring(TestCorpusIndex.CorpusDir.Length).TrimStart('/', '\\');
                        string target = Path.Combine(root, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(target));
                        File.WriteAllBytes(target, File.ReadAllBytes(source));
                    }
                    _stagedRoot = root;
                }
            }
            Assert.True(File.Exists(TemplateKey.ToPath(artifact.Templates[0].Key, _stagedRoot)),
                "Staging " + row.Name + ": the staged corpus has no file for its key.");
            return _stagedRoot;
        }

        /// <summary>One P1-R9 pass. The request options render under
        /// <see cref="PrecompiledMismatchPolicy.Strict"/> (<see cref="FallbackGuard.GuardedOptions"/>), so a
        /// gauntlet refusal throws rather than recompiling; the staged pass also turns the staleness step on.</summary>
        internal static RowResult RegisterRow(CorpusIntentRow row, string rootPath, string pass)
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
            ExpectedRefusals.RemoveAll(p => string.Equals(p.Key, row.Name, StringComparison.Ordinal));
            byte[] loaded = image;
            string requestRoot = rootPath;
            if (pass == FileBackedPass || pass == StagedPass)
            {
                string path = TestCorpusIndex.WrittenArtifactPath("compiled-form-" + SafeName(row.Name) + ".bin");
                File.WriteAllBytes(path, image);
                loaded = File.ReadAllBytes(path);
            }
            RegisterImage(loaded, "HeddleTestAsm_Parity" + row.Name.Replace(".", "_"));
            if (pass == StagedPass)
                requestRoot = StageEntry(row, back);
            var requestOptions = FallbackGuard.GuardedOptions(RequestOptions(row, requestRoot));
            requestOptions.EnableFileChangeCheck = pass == StagedPass;
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
                Assert.Fail("Materialization fault for " + row.Name + ": " + fault + ".");
            }
            return new RowResult { Pass = pass, Artifact = back, Image = loaded, Entry = entry, Strategy = strategy };
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

        /// <summary>Renders a materialized strategy through the adapter's three sinks — the successor of
        /// the deleted <c>PrecompiledRuntime.Generate*</c> roots, which likewise provisioned no locals
        /// frame of their own (the frame rides the strategy).</summary>
        internal static string RenderStrategy(IProcessStrategy strategy, object model) =>
            new HeddleTemplate(strategy).Generate(model);

        internal static void RenderStrategy(IProcessStrategy strategy, object model, TextWriter writer) =>
            new HeddleTemplate(strategy).Generate(model, writer);

        internal static void RenderStrategy(IProcessStrategy strategy, object model,
            IBufferWriter<byte> writer) =>
            new HeddleTemplate(strategy).Generate(model, writer);

        /// <summary>Renders both tiers through all three sinks and asserts byte identity, returning the
        /// shared output. The dynamic reference compiles without recording or deferral.</summary>
        internal static string AssertThreeSinkParity(CorpusIntentRow row, IProcessStrategy strategy,
            string rootPath) => AssertThreeSinkParity(row, strategy, rootPath, InMemoryPass);

        internal static string AssertThreeSinkParity(CorpusIntentRow row, IProcessStrategy strategy,
            string rootPath, string pass)
        {
            Type modelType;
            object model;
            var modelEx = ModelExFor(row, out modelType, out model);
            string text = CorpusText(row.Name);

            string s1 = RenderStrategy(strategy, model);
            string s2;
            using (var writer = new StringWriter())
            {
                RenderStrategy(strategy, model, writer);
                s2 = writer.ToString();
            }
            var seq = new Streaming.TestBufferWriter();   // ArrayBufferWriter<byte> is unavailable on net48 (see SinkTestWriters.cs).
            RenderStrategy(strategy, model, seq);
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
            var dseq = new Streaming.TestBufferWriter();
            dynamic.Generate(model, dseq);
            string d3 = Encoding.UTF8.GetString(dseq.WrittenSpan.ToArray());

            Assert.True(s1 == d1 && s2 == d2 && s3 == d3,
                "Byte divergence for " + row.Name + " (" + pass + " pass): string=" + (s1 == d1) +
                " writer=" + (s2 == d2) + " utf8=" + (s3 == d3) + ".");
            return s1;
        }
    }
}
