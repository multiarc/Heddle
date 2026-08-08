using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.TestCorpus;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The compiled-fragment tier for embedded C#: each expression is emitted as the engine's own one-line method
    /// shape into the generated file's <c>namespace Heddle.Runtime</c> block and compiled by the consumer's
    /// compiler — no runtime Roslyn, no whole-template degrade. These cases pin the constructs that degraded the
    /// corpus flagships before the fragment existed: a <c>@model(){{dynamic}}</c> document whose expressions are
    /// spelled <c>dynamic</c>, <c>root</c> reads at recursion depth, and self-recursive typed definitions.
    /// </summary>
    public class EmbeddedCSharpFragmentTests
    {
        private static readonly Dictionary<string, string> FullCSharpBuild =
            new Dictionary<string, string> { ["build_property.HeddleExpressionMode"] = "FullCSharp" };

        private static TemplateOptions Runtime => new TemplateOptions { ExpressionMode = ExpressionMode.FullCSharp };

        /// <summary>The engine binds dynamic operations through the runtime binder, observed off the loaded
        /// assembly set — an input, made explicit the way the harness loads model assemblies.</summary>
        private static void EnsureRuntimeBinder() => Assembly.Load("Microsoft.CSharp");

        private static (string key, string content) CorpusEntry(string name)
        {
            var templates = TestCorpusIndex.Load();
            var row = templates.Single(t => Path.GetFileName(t.key) == name);
            return (row.key, row.content);
        }

        /// <summary>A <c>Heddle.Tests.Data</c> tree built by reflection — the engine test project's model types are
        /// deliberately not a compile-time reference here, only a loaded assembly, exactly as they reach the
        /// dynamic tier.</summary>
        private static object CategoryList(string typeName)
        {
            var models = Assembly.LoadFrom(DifferentialHarness.EngineTestModelsDll());
            var categoryType = models.GetType("Heddle.Tests.Data." + typeName)
                               ?? throw new InvalidOperationException(typeName + " not found in Heddle.Tests");
            var complexType = categoryType.GetProperty("ComplexObject").PropertyType;
            var dataType = complexType.GetProperty("Data").PropertyType;
            // DynamicCategory declares its children ICollection<dynamic>; Category declares ICollection<Category>.
            var elementType = categoryType.GetProperty("SubCategories").PropertyType.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);

            object NewList(params object[] items)
            {
                var list = (IList) Activator.CreateInstance(listType);
                foreach (var item in items)
                    list.Add(item);
                return list;
            }

            object NewCategory(string name, string text, object subs)
            {
                var category = Activator.CreateInstance(categoryType);
                categoryType.GetProperty("Name").SetValue(category, name);
                categoryType.GetProperty("SubCategories").SetValue(category, subs ?? NewList());
                if (text != null)
                {
                    var data = Activator.CreateInstance(dataType);
                    dataType.GetProperty("Text").SetValue(data, text);
                    var complex = Activator.CreateInstance(complexType);
                    complexType.GetProperty("Data").SetValue(complex, data);
                    categoryType.GetProperty("ComplexObject").SetValue(category, complex);
                }

                return category;
            }

            return NewList(
                NewCategory("top1", "TEXT1", NewList(
                    NewCategory("mid1", null, NewList(
                        NewCategory("leaf1", "TEXT2", null))),
                    NewCategory("mid2", "TEXT3", null))),
                NewCategory("top2", null, null));
        }

        /// <summary>
        /// The corpus recursion flagship, byte-for-byte: a <c>@model(){{dynamic}}</c> document whose typed
        /// <c>:: Category</c> definition reads <c>@(@root.Count)</c> — the root parameter, spelled <c>dynamic</c>
        /// because the declared-dynamic root pins the engine's <c>RootScopeType</c> dynamic — recurses into itself
        /// through <c>@list</c>, and drives <c>@if</c> conditions off embedded C#. Every one of those degraded the
        /// whole template before the fragment tier.
        /// </summary>
        [Theory]
        [InlineData("recursion.heddle", "Category")]
        [InlineData("dynamic-recursion.heddle", "DynamicCategory")]
        public void TheCorpusRecursionFlagshipsPrecompileByteIdentically(string name, string modelTypeName)
        {
            EnsureRuntimeBinder();
            var (key, content) = CorpusEntry(name);
            var model = CategoryList(modelTypeName);

            var gen = DifferentialHarness.Generate(new[] { (key, content) }, FullCSharpBuild,
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var (pre, dyn) = DifferentialHarness.Render(key, content, null, model, FullCSharpBuild, Runtime,
                extraReferences: DifferentialHarness.EngineTestModelReferences());
            Assert.NotEqual(string.Empty, dyn);
            Assert.Equal(dyn, pre);
        }

        /// <summary>The cast-timing divergence the fragment call fixes, pinned by shape: the model conversion sits
        /// in the argument list of the fragment call — where the engine's <c>CompiledParameter</c> converts, at the
        /// call, after the preceding pieces are written — and the hoisted
        /// <c>var model = (T)scope.ModelData;</c> that made an <c>InvalidCastException</c> fire before the first
        /// piece is gone from the body entirely.</summary>
        [Fact]
        public void TheModelCastSitsAtTheFragmentCallNotHoistedAboveThePieces()
        {
            const string key = "views/fragment-cast-timing.heddle";
            const string template =
                "@model(){{Heddle.Generator.IntegrationTests.Fixtures.Catalog}}@\\\nbefore|@(@model.Title)|after\n";

            var gen = DifferentialHarness.Generate(new[] { (key, template) }, FullCSharpBuild);
            Assert.Empty(gen.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            DifferentialHarness.ExpectPrecompiled(gen, key);

            var source = gen.TemplateSources.Values.Single(s => s.Contains("__cs0"));
            var fragmentCall = source.Split('\n').First(l => l.Contains("__CSharp_") && l.Contains("RenderData"));
            Assert.Contains("(global::Heddle.Generator.IntegrationTests.Fixtures.Catalog)scope.ModelData", fragmentCall);
            Assert.DoesNotContain("var model", source);
        }
    }
}
