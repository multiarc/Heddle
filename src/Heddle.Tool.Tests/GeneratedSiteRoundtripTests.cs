using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Heddle.Tool.Compile;
using Xunit;

namespace Heddle.Tool.Tests
{
    public class RoundtripAddress
    {
        public string City { get; set; }
        public decimal Total { get; set; }
        public int Zip { get; set; }
    }

    public class RoundtripCustomer
    {
        public string Name { get; set; }
        public RoundtripAddress Address { get; set; }
        public int? Level { get; set; }
        public string Secret { internal get; set; }
    }

    public class RoundtripOrder
    {
        public RoundtripCustomer Customer { get; set; }
        public decimal Price { get; set; }
        public int Qty { get; set; }
        public double Ratio { get; set; }
        public string[] Tags { get; set; }
    }

    /// <summary>The printer's execution proof (P3-R3 through P3-R5, served through P3-R1): fixture
    /// templates compile through <c>heddle compile</c>, the printed source compiles in a consumer
    /// assembly, the loaded table serves every printable site (strict load renders without throwing),
    /// and the bytes equal the dynamic tier on all three sinks — over null and full models. A declined
    /// site (an internal getter, an engine-internal built-in call) is listed in HED7031, rebuilds from
    /// data, and still parities — but never under strict load, which names it instead of compiling it.</summary>
    public class GeneratedSiteRoundtripTests : IDisposable
    {
        private readonly string _dir;
        private readonly Heddle.Data.TemplateOptions _savedDefaultOptions;

        public GeneratedSiteRoundtripTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-sites-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _savedDefaultOptions = Heddle.Precompiled.PrecompiledTemplates.DefaultOptions;
        }

        public void Dispose()
        {
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            try
            {
                AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", false);
            }
            catch (Exception)
            {
            }

            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void MemberAndExpressionSitesServeFromTableWithParity()
        {
            string key = "rt-" + Guid.NewGuid().ToString("N");
            string member = WriteTemplate("member.heddle",
                "Hello @(Customer.Name)! City: @(Customer.Address.City). Total: @(Customer.Address.Total). Level: @(Customer.Level).\n");
            string expr = WriteTemplate("expr.heddle",
                "Sum: @(Price * 1.2m + Qty). Pick: @(Qty > 2 ? \"many\" : \"few\"). City: @(Customer.Address.City ?? \"none\"). Shout: @(upper(Customer.Name)) len @(len(Customer.Name)). First: @(Tags[0]). Mix: @(Ratio + Qty).\n");
            string model = typeof(RoundtripOrder).AssemblyQualifiedName;
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native",
                "--template", member + "|" + key + "-member||" + model + "|",
                "--template", expr + "|" + key + "-expr||" + model + "|",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.True(result.Exit == 0, "exit " + result.Exit + "\nSTDOUT:\n" + result.Stdout + "\nSTDERR:\n" + result.Stderr);
            // The member template prints whole; the expression template's two built-in calls decline per
            // P3-R2 (their target is engine-internal, and the 2.x shim façade is gone): HED7031 lists them.
            Assert.Contains("HED7031", result.Stdout);
            string source = File.ReadAllText(Out("gen.g.cs"));
            Assert.Contains("TryGetSite", source);
            Assert.Contains("ArtifactDigest =>", source);

            var loaded = CompileAndLoad(Out("gen.g.cs"), Out("form.bin"));
            Heddle.Precompiled.PrecompiledTemplates.Register(loaded);
            var full = FullModel();
            var sparse = new RoundtripOrder
            {
                Customer = new RoundtripCustomer(),
                Price = 9.5m,
                Qty = 1,
                Ratio = 0.5,
                Tags = new[] { "solo" }
            };
            AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", true);
            try
            {
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions =
                    new Heddle.Data.TemplateOptions("strict-roundtrip");
                // Strict load renders the decline-free member template: every site served from the table,
                // nothing compiled at load.
                AssertParity(loaded, key + "-member", MemberText(), full, "Text", "Native");
                AssertParity(loaded, key + "-member", MemberText(), sparse, "Text", "Native");
            }
            finally
            {
                AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", false);
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            }

            // The expression template's declined built-in sites rebuild from data at lax load and parity
            // on all three sinks — but strict load would name them instead of compiling them.
            AssertParity(loaded, key + "-expr", ExprText(), full, "Text", "Native");
            AssertParity(loaded, key + "-expr", ExprText(), sparse, "Text", "Native");
        }

        [Fact]
        public void CSharpSiteServesFromTableWithParity()
        {
            string key = "rt-" + Guid.NewGuid().ToString("N");
            string template = WriteTemplate("cs.heddle",
                "@using(){{System}}\nLoud: @(@model.Customer.Address.Total.ToString(\"F2\"))|@(@model.Qty + 1)\n");
            string model = typeof(RoundtripOrder).AssemblyQualifiedName;
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "FullCSharp",
                "--template", template + "|" + key + "-cs||" + model + "|",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.True(result.Exit == 0, "exit " + result.Exit + "\nSTDOUT:\n" + result.Stdout + "\nSTDERR:\n" + result.Stderr);
            Assert.DoesNotContain("HED7031", result.Stdout);
            string source = File.ReadAllText(Out("gen.g.cs"));
            Assert.Contains("ProcessData_S", source);

            var loaded = CompileAndLoad(Out("gen.g.cs"), Out("form.bin"));
            Heddle.Precompiled.PrecompiledTemplates.Register(loaded);
            AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", true);
            try
            {
                var strictCSharp = new Heddle.Data.TemplateOptions("strict-csharp");
                strictCSharp.ExpressionMode = Heddle.Data.ExpressionMode.FullCSharp;
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = strictCSharp;
                AssertParity(loaded, key + "-cs", CSharpText(), FullModel(), "Text", "FullCSharp");
            }
            finally
            {
                AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", false);
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            }
        }

        [Fact]
        public void DeclinedSiteIsListedAndRebuildsFromData()
        {
            string key = "rt-" + Guid.NewGuid().ToString("N");
            string template = WriteTemplate("secret.heddle", "Secret: @(Secret).\n");
            string model = typeof(RoundtripCustomer).AssemblyQualifiedName;
            string rsp = WriteRsp("--project", Project(), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native",
                "--template", template + "|" + key + "-secret||" + model + "|",
                "--artifact-out", Out("form.bin"), "--source-out", Out("gen.g.cs"),
                "--stamp", Out("stamp.txt"));
            var result = Run("compile", "@" + rsp);
            Assert.True(result.Exit == 0, "exit " + result.Exit + "\nSTDOUT:\n" + result.Stdout + "\nSTDERR:\n" + result.Stderr);
            Assert.Contains("HED7031", result.Stdout);
            Assert.Contains("rebuilt at load", result.Stdout);

            // The declined site still renders through the data path, byte-identical.
            var loaded = CompileAndLoad(Out("gen.g.cs"), Out("form.bin"));
            Heddle.Precompiled.PrecompiledTemplates.Register(loaded);
            var customer = new RoundtripCustomer { Name = "Ada", Secret = "s3cr3t" };
            AssertParity(loaded, key + "-secret", SecretText(), customer, "Text", "Native");

            // ... but strict load names it instead of compiling it. A fresh bind (the
            // wrapper caches its first bind) under strict options throws from BindTyped.
            AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", true);
            try
            {
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions =
                    new Heddle.Data.TemplateOptions("strict-decline");
                var ex = Assert.ThrowsAny<Exception>(() =>
                    Heddle.Precompiled.PrecompiledTemplates.BindTyped(loaded, key + "-secret",
                        typeof(RoundtripCustomer)));
                Assert.Equal("Heddle.Precompiled.PrecompiledStrictLoadException",
                    ex.GetType().FullName);
            }
            finally
            {
                AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", false);
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            }
        }

        private static string MemberText() =>
            "Hello @(Customer.Name)! City: @(Customer.Address.City). Total: @(Customer.Address.Total). Level: @(Customer.Level).\n";

        private static string ExprText() =>
            "Sum: @(Price * 1.2m + Qty). Pick: @(Qty > 2 ? \"many\" : \"few\"). City: @(Customer.Address.City ?? \"none\"). Shout: @(upper(Customer.Name)) len @(len(Customer.Name)). First: @(Tags[0]). Mix: @(Ratio + Qty).\n";

        private static string CSharpText() =>
            "@using(){{System}}\nLoud: @(@model.Customer.Address.Total.ToString(\"F2\"))|@(@model.Qty + 1)\n";

        private static string SecretText() => "Secret: @(Secret).\n";

        private static RoundtripOrder FullModel() => new RoundtripOrder
        {
            Customer = new RoundtripCustomer
            {
                Name = "Ada",
                Address = new RoundtripAddress { City = "Minsk", Total = 129.50m, Zip = 220030 },
                Level = 7,
                Secret = "s3cr3t"
            },
            Price = 100m,
            Qty = 3,
            Ratio = 1.5,
            Tags = new[] { "a", "b" }
        };

        private string Project() => Path.Combine(_dir, "proj.csproj");

        private string Out(string name) => Path.Combine(_dir, "out", name);

        private string WriteTemplate(string name, string text)
        {
            string path = Path.Combine(_dir, name);
            File.WriteAllText(path, text);
            return path;
        }

        private string WriteRsp(params string[] lines)
        {
            string path = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".rsp");
            File.WriteAllLines(path, lines);
            return path;
        }

        private sealed class RunResult
        {
            internal int Exit;
            internal string Stdout;
            internal string Stderr;
        }

        private static RunResult Run(params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exit = Program.Run(args, stdout, stderr);
            return new RunResult { Exit = exit, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
        }

        /// <summary>Compiles the printed source as a consumer assembly would: the generated code
        /// plus the artifact beside it as the <c>Heddle.CompiledForm</c> resource.</summary>
        private static Assembly CompileAndLoad(string sourcePath, string artifactPath)
        {
            string source = File.ReadAllText(sourcePath);
            var tree = CSharpSyntaxTree.ParseText(source);
            var references = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                string location = null;
                try
                {
                    location = loaded.Location;
                }
                catch (NotSupportedException)
                {
                }

                if (string.IsNullOrEmpty(location) || !seen.Add(location))
                    continue;
                references.Add(MetadataReference.CreateFromFile(location));
            }
            var compilation = CSharpCompilation.Create("HeddleRoundtrip_" + Guid.NewGuid().ToString("N"),
                new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release));
            var image = new MemoryStream();
            var emit = compilation.Emit(image, manifestResources: new[]
            {
                new ResourceDescription("Heddle.CompiledForm",
                    () => File.OpenRead(artifactPath), true)
            });
            Assert.True(emit.Success,
                "Printed source did not compile: " + string.Join("\n",
                    emit.Diagnostics.Select(d => d.ToString()).ToArray()));
            return Assembly.Load(image.ToArray());
        }

        private static Heddle.HeddleTemplate DynamicTemplate(string text, object model, string mode)
        {
            var options = new Heddle.Data.TemplateOptions("roundtrip");
            options.OutputProfile = Heddle.Data.OutputProfile.Text;
            if (string.Equals(mode, "FullCSharp", StringComparison.Ordinal))
                options.ExpressionMode = Heddle.Data.ExpressionMode.FullCSharp;
            var modelType = model != null ? model.GetType() : typeof(object);
            var context = new Heddle.Runtime.CompileContext(options,
                new Heddle.Data.ExType(modelType));
            return new Heddle.HeddleTemplate(text, context);
        }

        private static Type WrapperType(Assembly loaded, string key)
        {
            var wrapper = loaded.GetType("Heddle.Generated." + SanitizeName.ForKey(key));
            if (wrapper == null)
                throw new InvalidOperationException("No wrapper class for '" + key + "'.");
            return wrapper;
        }

        private static System.Reflection.MethodInfo StringOverload(Type wrapper)
        {
            foreach (var method in wrapper.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Generate" || method.ReturnType != typeof(string))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 3)
                    return method;
            }

            throw new InvalidOperationException("No string Generate overload on '" + wrapper.FullName + "'.");
        }

        private static System.Reflection.MethodInfo WriterOverload(Type wrapper)
        {
            foreach (var method in wrapper.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Generate" || method.ReturnType != typeof(void))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 4 && parameters[1].ParameterType == typeof(TextWriter))
                    return method;
            }

            throw new InvalidOperationException("No writer Generate overload on '" + wrapper.FullName + "'.");
        }

        private static System.Reflection.MethodInfo BufferOverload(Type wrapper)
        {
            foreach (var method in wrapper.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.Name != "Generate" || method.ReturnType != typeof(void))
                    continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 4 && parameters[1].ParameterType == typeof(IBufferWriter<byte>))
                    return method;
            }

            throw new InvalidOperationException("No buffer Generate overload on '" + wrapper.FullName + "'.");
        }

        private static string RenderString(Assembly loaded, string key, object model)
        {
            var wrapper = WrapperType(loaded, key);
            return (string)StringOverload(wrapper).Invoke(null, new[] { model, null, null });
        }

        private static void AssertParity(Assembly loaded, string key, string text, object model,
            string profile, string mode)
        {
            string expected = DynamicTemplate(text, model, mode).Generate(model);
            string actual = RenderString(loaded, key, model);
            Assert.Equal(expected, actual);

            var wrapper = WrapperType(loaded, key);
            var writerMethod = WriterOverload(wrapper);
            var expectedWriter = new StringWriter();
            DynamicTemplate(text, model, mode).Generate(model, expectedWriter);
            var actualWriter = new StringWriter();
            writerMethod.Invoke(null, new object[] { model, actualWriter, null, null });
            Assert.Equal(expectedWriter.ToString(), actualWriter.ToString());

            var bufferMethod = BufferOverload(wrapper);
            var expectedBuffer = new ArrayBufferWriter<byte>();
            DynamicTemplate(text, model, mode).Generate(model, expectedBuffer);
            var actualBuffer = new ArrayBufferWriter<byte>();
            bufferMethod.Invoke(null, new object[] { model, actualBuffer, null, null });
            Assert.Equal(expectedBuffer.WrittenSpan.ToArray(), actualBuffer.WrittenSpan.ToArray());
        }
    }
}
