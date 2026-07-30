using System;
using System.Collections.Generic;
using System.Linq;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Declaratively exported host functions bind <b>directly</b> to their discovered container (no shim, no runtime
    /// registry). The generator discovers <c>[ExportFunctions]</c> over the compilation's reference to this test
    /// assembly; the dynamic backend renders the same <c>MethodInfo</c> after a <c>RegisterFrom</c> at startup —
    /// differential-gated byte-for-byte.
    /// </summary>
    public class ExportFunctionTests
    {
        private const string ProductType = "Heddle.Generator.IntegrationTests.Fixtures.Product";

        private static TemplateOptions OptionsWithExports()
        {
            var options = new TemplateOptions();
            var registry = new FunctionRegistry();
            registry.RegisterFrom(typeof(TemplateFunctions).Assembly);
            options.Functions = registry;
            return options;
        }

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model,
                runtimeOptions: OptionsWithExports());
            Assert.Equal(dyn, precompiled);
        }

        public static IEnumerable<object[]> Products()
        {
            yield return new object[] { new Product { Name = "wonder widget" } };
            yield return new object[] { new Product { Name = "" } };
            yield return new object[] { new Product { Name = null } };
            yield return new object[] { null };
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void ExportedFunctionDirectBinding(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(titlecase(Name))</span>\n";
            AssertParity("views/label-export.heddle", t, typeof(Product), model);
        }

        [Theory]
        [MemberData(nameof(Products))]
        public void ExportedAndDefaultFunctionsInOneTemplate(Product model)
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(upper(Name)) - @(titlecase(Name)) - @(shout(Name))</span>\n";
            AssertParity("views/label-mixed.heddle", t, typeof(Product), model);
        }

        /// <summary>
        /// A host export whose return type is not one of the primitives the shared operand descriptor can name. The
        /// gates that type a call-site value — <c>@list</c>'s accepted type, an <c>@out</c> value against its slot
        /// type — went through that descriptor, so every such export answered "cannot say" and was exempted; the
        /// engine has the declared return type in hand and refuses both templates by name. The container lives in an
        /// assembly of this suite's own so no shared export fixture moves.
        /// </summary>
        public sealed class NonPrimitiveReturns
        {
            private const string Source = @"
[assembly: Heddle.Attributes.ExportFunctions(typeof(IsolatedExports.Fns))]
namespace IsolatedExports
{
    public sealed class Box { public string Label { get; set; } public override string ToString() => Label; }
    public static class Fns
    {
        public static Box Pack(string left, string right) => new Box { Label = left + right };
        public static System.DateTime At(int year, int month) => new System.DateTime(year, month, 1);
        public static string[] Split(string left, string right) => new[] { left, right };
        public static dynamic Loose(int left, int right) => (left + right).ToString();
    }
}";

            /// <summary>The container assembly, emitted once and reused: the file name carries the source's hash, so
            /// a run never loads a stale build and never leaves a new file behind for each run.</summary>
            private static readonly Lazy<System.Reflection.Assembly> Container =
                new Lazy<System.Reflection.Assembly>(Build);

            private static string _path;

            private static System.Reflection.Assembly Build()
            {
                unchecked
                {
                    int hash = 17;
                    foreach (var c in Source)
                        hash = hash * 31 + c;
                    var name = "HeddleIsolatedExports" + hash.ToString("x8");
                    _path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), name + ".dll");
                    if (!System.IO.File.Exists(_path))
                    {
                        var references = ((string) AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))
                            .Split(System.IO.Path.PathSeparator)
                            .Where(p => !string.IsNullOrEmpty(p) && System.IO.File.Exists(p))
                            .Select(p => (Microsoft.CodeAnalysis.MetadataReference)
                                Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p))
                            .ToList();
                        references.Add(Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(
                            typeof(HeddleTemplate).Assembly.Location));
                        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(name,
                            new[] { Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(Source) }, references,
                            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
                        using (var stream = System.IO.File.Create(_path))
                        {
                            var emit = compilation.Emit(stream);
                            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
                        }
                    }

                    return System.Reflection.Assembly.LoadFrom(_path);
                }
            }

            private static IReadOnlyList<Microsoft.CodeAnalysis.MetadataReference> References()
            {
                var unused = Container.Value;
                return new[] { Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(_path) };
            }

            private static TemplateOptions RuntimeOptions()
            {
                var options = new TemplateOptions();
                var registry = new FunctionRegistry();
                registry.RegisterFrom(Container.Value);
                options.Functions = registry;
                return options;
            }

            private static void AssertEngineRefuses(string template, string expected)
            {
                var dynamicTemplate = new HeddleTemplate(template,
                    new Heddle.Runtime.CompileContext(RuntimeOptions(), typeof(Product)));
                Assert.False(dynamicTemplate.CompileResult.Success);
                Assert.Contains(expected, dynamicTemplate.CompileResult.ToString(), StringComparison.Ordinal);
            }

            /// <summary>A user class and a <c>DateTime</c> in <c>@list</c> position: neither is enumerable, and the
            /// engine names the return type it refused.</summary>
            [Theory]
            [InlineData("class", "pack(\"a\", \"b\")", "IsolatedExports.Box")]
            [InlineData("datetime", "at(2020, 1)", "System.DateTime")]
            public void AnExportReturningANonPrimitiveIsRefusedByListOnBothTiers(string name, string call,
                string returned)
            {
                var key = "views/export-return-list-" + name + ".heddle";
                var t = "@model(){{" + ProductType + "}}@\\\n@list(" + call + "){{<@()>}}\n";

                DifferentialHarness.ExpectDegrade(
                    DifferentialHarness.Generate(new[] { (key, t) }, extraReferences: References()), key);
                AssertEngineRefuses(t, returned);
            }

            /// <summary>The same two return types as a slot value, where the rule is assignability rather than
            /// enumerability. Each takes the slot type its own refusal needs: a <c>DateTime</c> into <c>object</c> is
            /// the boxing the engine's table switches off for this check, and the host's class into a
            /// <c>string</c> slot is the plain unrelated-type refusal — a <c>Box</c> into <c>object</c> would be an
            /// ordinary reference conversion that both tiers accept, and is the near neighbour below.</summary>
            [Theory]
            [InlineData("class", "pack(\"a\", \"b\")", "IsolatedExports.Box", "string", "System.String")]
            [InlineData("datetime", "at(2020, 1)", "System.DateTime", "object", "System.Object")]
            public void AnExportReturningANonPrimitiveIsCheckedAgainstTheSlotType(string name, string call,
                string returned, string slotType, string slotClr)
            {
                var key = "views/export-return-slot-" + name + ".heddle";
                var t = "@model(){{" + ProductType + "}}@%\n<s(out:: " + slotType + ")>{{[@out(" + call + ")]}} :: " +
                        ProductType + "\n%@\n@s(this){{|@()|}}\n";

                DifferentialHarness.ExpectDegrade(
                    DifferentialHarness.Generate(new[] { (key, t) }, extraReferences: References()), key);
                AssertEngineRefuses(t,
                    "The slot value type " + returned + " is not assignable to the declared slot parameter type " +
                    slotClr + ".");
            }

            /// <summary>
            /// An export declared to return <c>dynamic</c>. There is no such thing in metadata — the engine reads a
            /// <c>MethodInfo</c> whose return type is <c>System.Object</c> — so this is the one return type whose
            /// symbol the emitter must not take at face value: read as a dynamic value it would be refused outright
            /// for having no static type, and the object slot below is one the engine takes.
            /// </summary>
            [Theory]
            [InlineData("object", "object", true)]
            [InlineData("string", "string", false)]
            public void AnExportReturningDynamicIsTypedAsObjectLikeTheEngineTypesIt(string name, string slotType,
                bool precompiles)
            {
                var key = "views/export-return-dynamic-" + name + ".heddle";
                var t = "@model(){{" + ProductType + "}}@%\n<s(out:: " + slotType + ")>{{[@out(loose(1, 2))]}} :: " +
                        ProductType + "\n%@\n@s(this){{|@()|}}\n";

                if (precompiles)
                {
                    var (pre, dyn) = DifferentialHarness.Render(key, t, typeof(Product), new Product { Name = "n" },
                        runtimeOptions: RuntimeOptions(), extraReferences: References());
                    Assert.Equal(dyn, pre);
                    Assert.Equal("[|3|]\n", dyn);
                }
                else
                {
                    DifferentialHarness.ExpectDegrade(
                        DifferentialHarness.Generate(new[] { (key, t) }, extraReferences: References()), key);
                    AssertEngineRefuses(t, "The slot value type System.Object is not assignable to the declared " +
                                           "slot parameter type System.String.");
                }
            }

            /// <summary>The near neighbours, and the reason this is a return-type rule rather than a refusal of
            /// exports: an export returning an array enumerates on both tiers, and one returning a <c>Box</c> into a
            /// slot that takes a <c>Box</c> projects on both tiers — same syntax, same container, both precompiled.
            /// </summary>
            [Fact]
            public void AnExportWhoseReturnTypeFitsStillPrecompiles()
            {
                const string listKey = "views/export-return-list-ok.heddle";
                const string listTemplate = "@model(){{" + ProductType + "}}@\\\n" +
                                            "@list(split(\"a\", \"b\")){{<@()>}}\n";

                var (listPre, listDyn) = DifferentialHarness.Render(listKey, listTemplate, typeof(Product),
                    new Product { Name = "n" }, runtimeOptions: RuntimeOptions(),
                    extraReferences: References());
                Assert.Equal(listDyn, listPre);
                Assert.Equal("<a><b>\n", listDyn);

                const string slotKey = "views/export-return-slot-ok.heddle";
                const string slotTemplate = "@model(){{" + ProductType + "}}@%\n" +
                                            "<s(out:: IsolatedExports.Box)>{{[@out(pack(\"a\", \"b\"))]}} :: " +
                                            ProductType + "\n%@\n@s(this){{|@(Label)|}}\n";

                var (slotPre, slotDyn) = DifferentialHarness.Render(slotKey, slotTemplate, typeof(Product),
                    new Product { Name = "n" }, runtimeOptions: RuntimeOptions(),
                    extraReferences: References());
                Assert.Equal(slotDyn, slotPre);
                Assert.Equal("[|ab|]\n", slotDyn);

                // The reference conversion the engine's table does allow: the same class into an `object` slot.
                const string objectKey = "views/export-return-slot-object.heddle";
                const string objectTemplate = "@model(){{" + ProductType + "}}@%\n" +
                                              "<s(out:: object)>{{[@out(pack(\"a\", \"b\"))]}} :: " +
                                              ProductType + "\n%@\n@s(this){{|@()|}}\n";

                var (objectPre, objectDyn) = DifferentialHarness.Render(objectKey, objectTemplate, typeof(Product),
                    new Product { Name = "n" }, runtimeOptions: RuntimeOptions(),
                    extraReferences: References());
                Assert.Equal(objectDyn, objectPre);
                Assert.Equal("[|ab|]\n", objectDyn);
            }
        }

        [Fact]
        public void ExportRowRecordedInManifest()
        {
            var t = "@model(){{" + ProductType + "}}@\\\n<span>@(titlecase(Name))</span>\n";
            var gen = DifferentialHarness.Generate(new[] { ("views/label-export.heddle", t) });
            Assert.NotNull(gen.ManifestSource);
            Assert.Contains("titlecase", gen.ManifestSource);
            // Direct-bound to the discovered container as AQN sans version (not the shim target).
            Assert.Contains("Heddle.Generator.IntegrationTests.Fixtures.TemplateFunctions, Heddle.Generator.IntegrationTests",
                gen.ManifestSource);
        }
    }
}
