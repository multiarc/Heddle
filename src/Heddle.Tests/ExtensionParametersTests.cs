using System;
using System.Linq;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 8 (WI9) — the dynamic-tier extension-parameter matrix: the <c>[Prop]</c> declaration surface, the
    /// relaxed HED5005 gate, the reused call-time HED5001–HED5004 and declaration-side
    /// HED5007/HED5008/HED5009/HED5010/HED5015, call-site symmetry with definition props, the sandbox gate, and
    /// the F6 encoding-preservation rows. Errors are asserted as positioned diagnostics, never bare failures.
    /// </summary>
    public class ExtensionParametersTests
    {
        public class Root
        {
            public string Photos { get; set; }
            public int Cols { get; set; }
        }

        private static Root Model() => new Root { Photos = "photos", Cols = 7 };

        // ---- Fixtures (registered by name through the public AddExtensions seam, the established pattern) ----

        [Prop("columns", typeof(int), Default = 3)]
        public sealed class GridExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope)
            {
                var columns = (int) scope.GetParameter("columns");
                return "cols=" + columns + ":" + (scope.ModelData?.ToString() ?? string.Empty);
            }

            public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
        }

        [Prop("span", typeof(int))]
        public sealed class GridReqExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) =>
                "span=" + scope.GetParameter("span") + ":" + (scope.ModelData?.ToString() ?? string.Empty);

            public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
        }

        public sealed class PlainEchoExtension : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => scope.ModelData?.ToString() ?? string.Empty;

            public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
        }

        public abstract class EchoBase : AbstractExtension
        {
            public override object ProcessData(in Scope scope) => scope.ModelData?.ToString() ?? string.Empty;

            public override void RenderData(in Scope scope) => scope.Renderer.Render((string) ProcessData(scope));
        }

        [Prop("a", typeof(int))]
        [Prop("a", typeof(string))]
        public sealed class DupPropExtension : EchoBase { }

        [Prop("out", typeof(int))]
        public sealed class ReservedPropExtension : EchoBase { }

        [Prop(null, typeof(int))]
        public sealed class NullNamePropExtension : EchoBase { }

        [Prop("a", typeof(int), Default = "x")]
        public sealed class BadDefaultExtension : EchoBase { }

        [Prop("a", typeof(System.Collections.Generic.List<>))]
        public sealed class BadTypeExtension : EchoBase { }

        [Prop("item", typeof(string))]
        public abstract class StringItemBase : EchoBase { }

        [Prop("item", typeof(object))]
        public sealed class WideningItemExtension : StringItemBase { }

        [Prop("n", typeof(object), Optional = true)]
        public abstract class ObjectItemBase : EchoBase { }

        [Prop("n", typeof(string), Default = "narrowed")]
        public sealed class NarrowItemExtension : ObjectItemBase
        {
            public override object ProcessData(in Scope scope) => "n=" + (scope.GetParameter("n") ?? "null");
        }

        [Prop("c", typeof(IComparable), Optional = true)]
        public abstract class IfaceBase : EchoBase { }

        [Prop("c", typeof(int?))]
        public sealed class NullableIfaceItemExtension : IfaceBase { }

        [Prop("m", typeof(int), Default = 1)]
        public abstract class IntBase : EchoBase { }

        [Prop("m", typeof(int?))]
        public sealed class NullableWidenItemExtension : IntBase { }

        [Prop("n", typeof(int?), Optional = true)]
        public abstract class NullableIntBase : EchoBase { }

        [Prop("n", typeof(int), Default = 5)]
        public sealed class NullableNarrowItemExtension : NullableIntBase
        {
            public override object ProcessData(in Scope scope) => "n=" + scope.GetParameter("n");
        }

        [EncodeOutput]
        [Prop("columns", typeof(int), Default = 3)]
        public sealed class EncodedGridExtension : AbstractHtmlExtension
        {
            protected override object ProcessDataInternal(in Scope scope) =>
                "<grid cols=" + scope.GetParameter("columns") + ">" + (scope.ModelData?.ToString() ?? string.Empty) +
                "</grid>";

            protected override void RenderDataInternal(in Scope scope) =>
                scope.Renderer.Render((string) ProcessDataInternal(scope));
        }

        /// <summary>The [Prop]-less twin of <see cref="EncodedGridExtension"/> with the identical output shape
        /// (columns pinned to the default 3) — the F6 byte-identity companion.</summary>
        [EncodeOutput]
        public sealed class EncodedPlainExtension : AbstractHtmlExtension
        {
            protected override object ProcessDataInternal(in Scope scope) =>
                "<grid cols=3>" + (scope.ModelData?.ToString() ?? string.Empty) + "</grid>";

            protected override void RenderDataInternal(in Scope scope) =>
                scope.Renderer.Render((string) ProcessDataInternal(scope));
        }

        /// <summary>White-box frozen-array identity probe: captures the carried values array reference so the
        /// all-constant zero-alloc claim (shared frozen array across renders) is observable.</summary>
        [Prop("columns", typeof(int), Default = 3)]
        public sealed class CaptureParamsExtension : AbstractExtension
        {
            [ThreadStatic] public static object[] LastValues;

            public override object ProcessData(in Scope scope)
            {
                LastValues = scope.ExtensionParameterValues;
                return string.Empty;
            }

            public override void RenderData(in Scope scope) => ProcessData(scope);
        }

        private static void EnsureRegistered()
        {
            Register("p8grid", typeof(GridExtension));
            Register("p8gridReq", typeof(GridReqExtension));
            Register("p8plain", typeof(PlainEchoExtension));
            Register("p8dup", typeof(DupPropExtension));
            Register("p8reserved", typeof(ReservedPropExtension));
            Register("p8nullname", typeof(NullNamePropExtension));
            Register("p8baddefault", typeof(BadDefaultExtension));
            Register("p8badtype", typeof(BadTypeExtension));
            Register("p8widen", typeof(WideningItemExtension));
            Register("p8narrow", typeof(NarrowItemExtension));
            Register("p8nulliface", typeof(NullableIfaceItemExtension));
            Register("p8nullwiden", typeof(NullableWidenItemExtension));
            Register("p8nullnarrow", typeof(NullableNarrowItemExtension));
            Register("p8encodedGrid", typeof(EncodedGridExtension));
            Register("p8encodedPlain", typeof(EncodedPlainExtension));
            Register("p8capture", typeof(CaptureParamsExtension));
        }

        private static void Register(string name, Type type)
        {
            if (!TemplateFactory.Exists(name))
                TemplateFactory.AddExtensions(new[] { new ExtensionType(name, type, false) });
        }

        private static HeddleTemplate Compile(string document, TemplateOptions options = null)
        {
            EnsureRegistered();
            return new HeddleTemplate(document, new CompileContext(options ?? new TemplateOptions(), typeof(Root)));
        }

        private static void AssertError(HeddleTemplate template, string diagnosticId)
        {
            Assert.False(template.CompileResult.Success);
            Assert.Contains(template.CompileResult.Errors, e => e.DiagnosticId == diagnosticId);
            Assert.All(template.CompileResult.Errors.Where(e => e.DiagnosticId == diagnosticId),
                e => Assert.NotEqual(default, e.Position));
        }

        // ---- Success rows ----

        [Fact]
        public void NamedArgumentBindsAndReadsViaGetParameter()
        {
            var t = Compile("-@p8grid(Photos, columns: 4)");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("-cols=4:photos", t.Generate(Model()));
        }

        [Fact]
        public void OmittedOptionalParameterBindsDefault()
        {
            var t = Compile("-@p8grid(Photos)");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("-cols=3:photos", t.Generate(Model()));
        }

        [Fact]
        public void DynamicArgumentEvaluatesAgainstCallerView()
        {
            var t = Compile("-@p8grid(Photos, columns: Cols)");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("-cols=7:photos", t.Generate(Model()));
        }

        // ---- Validation rows (reused call-time ids) ----

        [Fact]
        public void ParameterLessExtensionWithNamedArgsIsHed5005()
        {
            var t = Compile("-@p8plain(Photos, p: 1)");
            AssertError(t, HeddleDiagnosticIds.NamedArgumentsNotSupported);
        }

        [Fact]
        public void UnknownParameterIsHed5001()
        {
            var t = Compile("-@p8grid(Photos, rows: 4)");
            AssertError(t, HeddleDiagnosticIds.UnknownProp);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.UnknownProp && e.Error.Contains("extension 'p8grid'") &&
                     e.Error.Contains("columns"));
        }

        [Fact]
        public void MistypedArgumentIsHed5003()
        {
            var t = Compile("-@p8grid(Photos, columns: \"x\")");
            AssertError(t, HeddleDiagnosticIds.PropTypeMismatch);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.PropTypeMismatch &&
                     e.Error.Contains("extension 'p8grid'"));
        }

        [Fact]
        public void DuplicateArgumentIsHed5004()
        {
            var t = Compile("-@p8grid(Photos, columns: 4, columns: 5)");
            AssertError(t, HeddleDiagnosticIds.DuplicatePropArgument);
        }

        [Fact]
        public void MissingRequiredParameterIsHed5002()
        {
            var t = Compile("-@p8gridReq(Photos)");
            AssertError(t, HeddleDiagnosticIds.MissingRequiredProp);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.MissingRequiredProp &&
                     e.Error.Contains("extension 'p8gridReq'"));
        }

        // ---- Malformed-declaration rows (reused declaration-side ids, positioned at the call) ----

        [Theory]
        [InlineData("-@p8dup(Photos)", HeddleDiagnosticIds.DuplicatePropDeclaration)]
        [InlineData("-@p8reserved(Photos)", HeddleDiagnosticIds.ReservedPropName)]
        [InlineData("-@p8nullname(Photos)", HeddleDiagnosticIds.ReservedPropName)]
        [InlineData("-@p8baddefault(Photos)", HeddleDiagnosticIds.PropDefaultNotConvertible)]
        [InlineData("-@p8badtype(Photos)", HeddleDiagnosticIds.UnresolvedPropType)]
        public void MalformedPropDeclarationDrawsMappedId(string template, string id)
        {
            AssertError(Compile(template), id);
        }

        // ---- Inherited re-declaration rows ----

        [Fact]
        public void WideningRedeclarationIsHed5008()
        {
            var t = Compile("-@p8widen(Photos)");
            AssertError(t, HeddleDiagnosticIds.PropRedeclarationMismatch);
        }

        [Fact]
        public void NarrowingRedeclarationCompilesCleanAndRedefaults()
        {
            var t = Compile("-@p8narrow(Photos)");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("-n=narrowed", t.Generate(Model()));
        }

        // ---- Nullable<T> re-declaration (both directions — the cross-tier oracle) ----

        [Fact]
        public void NullableInterfaceRedeclarationIsHed5008()
        {
            AssertError(Compile("-@p8nulliface(Photos)"), HeddleDiagnosticIds.PropRedeclarationMismatch);
        }

        [Fact]
        public void NullableWideningRedeclarationIsHed5008()
        {
            AssertError(Compile("-@p8nullwiden(Photos)"), HeddleDiagnosticIds.PropRedeclarationMismatch);
        }

        [Fact]
        public void NullableNarrowingRedeclarationCompilesCleanAndRedefaults()
        {
            var t = Compile("-@p8nullnarrow(Photos)");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("-n=5", t.Generate(Model()));
        }

        // ---- Call-site symmetry with definition props ----

        private const string GridDef =
            "@% <gridDef(columns: int = 3)>{{cols=@(columns)}} :: System.String %@\n";

        [Theory]
        [InlineData("(Photos, columns: 4)", null)]
        [InlineData("(Photos, rows: 4)", HeddleDiagnosticIds.UnknownProp)]
        [InlineData("(Photos, columns: \"x\")", HeddleDiagnosticIds.PropTypeMismatch)]
        [InlineData("(Photos, columns: 4, columns: 5)", HeddleDiagnosticIds.DuplicatePropArgument)]
        public void DefinitionAndExtensionAcceptOrDiagnoseIdentically(string callArgs, string expectedId)
        {
            var defTemplate = Compile(GridDef + "@gridDef" + callArgs);
            var extTemplate = Compile("@p8grid" + callArgs);

            if (expectedId == null)
            {
                Assert.True(defTemplate.CompileResult.Success, defTemplate.CompileResult.ToString());
                Assert.True(extTemplate.CompileResult.Success, extTemplate.CompileResult.ToString());
            }
            else
            {
                AssertError(defTemplate, expectedId);
                AssertError(extTemplate, expectedId);
            }
        }

        // ---- Sandbox-negative ----

        [Fact]
        public void MemberPathsOnlyModeRejectsNamedArgumentsWithHed1014()
        {
            var t = Compile("-@p8grid(Photos, columns: 4)",
                new TemplateOptions { ExpressionMode = ExpressionMode.MemberPathsOnly });
            AssertError(t, HeddleDiagnosticIds.NativeExpressionsDisabled);
        }

        // ---- Encoding preserved (F6) ----

        [Fact]
        public void EncodeOutputExtensionWithPropStillEncodesByteIdenticallyToPropLessTwin()
        {
            var withProp = Compile("@p8encodedGrid(Photos)", new TemplateOptions { OutputProfile = OutputProfile.Html });
            var withoutProp = Compile("@p8encodedPlain(Photos)", new TemplateOptions { OutputProfile = OutputProfile.Html });
            Assert.True(withProp.CompileResult.Success, withProp.CompileResult.ToString());
            Assert.True(withoutProp.CompileResult.Success, withoutProp.CompileResult.ToString());

            var a = withProp.Generate(Model());
            var b = withoutProp.Generate(Model());
            Assert.Equal(b, a);                              // byte-identical to the same extension without [Prop]
            Assert.Contains("&lt;grid cols=3&gt;photos&lt;/grid&gt;", a);   // still HTML-encoded
        }

        [Fact]
        public void EncodeOutputExtensionWithPropStillDrawsHed2003InUnnamedChain()
        {
            var t = Compile("@(p8encodedGrid(Photos))", new TemplateOptions { OutputProfile = OutputProfile.Html });
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Contains(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.RedundantEncodingExtension);
        }

        // ---- Scope accessor contract + frozen-array identity ----

        [Fact]
        public void TryGetParameterReturnsFalseWithoutFrame()
        {
            Assert.False(Scope.Null.TryGetParameter("columns", out var value));
            Assert.Null(value);
        }

        [Fact]
        public void GetParameterThrowsForUndeclaredNameAndNoFrame()
        {
            Assert.Throws<ArgumentException>(() => Scope.Null.GetParameter("columns"));
        }

        [Fact]
        public void AllConstantParameterSetSharesTheFrozenArrayAcrossRenders()
        {
            var t = Compile("@p8capture(Photos, columns: 4)");
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            t.Generate(Model());
            var first = CaptureParamsExtension.LastValues;
            t.Generate(Model());
            var second = CaptureParamsExtension.LastValues;

            Assert.NotNull(first);
            Assert.Equal(4, Assert.IsType<int>(first[0]));
            Assert.Same(first, second);   // zero per-render allocation — the shared frozen prototype
        }
    }
}
