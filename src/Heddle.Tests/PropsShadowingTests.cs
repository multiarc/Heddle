using System;
using System.Dynamic;
using System.Reflection;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Body prop resolution: props win over model members (with the HED5011 shadowing warning),
    /// <c>this.&lt;name&gt;</c> is the explicit model escape, <c>::</c> root refs skip props, and prop reads stay
    /// statically typed even in a <c>:: dynamic</c> definition.
    /// </summary>
    public class PropsShadowingTests
    {
        private static HeddleTemplate Compile(string document, ExType modelType)
        {
            HeddleTemplate.Configure(typeof(PropsShadowingTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(new TemplateOptions(), modelType));
        }

        [Fact]
        public void PropWinsAndWarnsAndThisEscapes()
        {
            // Props win over model members; reach the member via this.<name>.
            const string doc =
                "@% <panel(style: string = \"PROP\")>{{[@(style)|@(this.style)|@(::style)]}} :: PropRoot %@\n@panel(this)";
            var t = Compile(doc, typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            var warning = Assert.Single(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.PropShadowsModelMember);
            Assert.Contains("this.style", warning.Fix);

            var model = new PropRoot { style = "MODEL" };
            Assert.Equal("[PROP|MODEL|MODEL]", t.Generate(model).Trim());
        }

        [Fact]
        public void NonShadowingPropDoesNotWarn()
        {
            const string doc = "@% <panel(tone: string = \"T\")>{{@(tone)}} :: PropArticle %@\n@panel(Article)";
            var t = Compile(doc, typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.DoesNotContain(t.Context.CompileWarnings,
                w => w.DiagnosticId == HeddleDiagnosticIds.PropShadowsModelMember);
        }

        [Fact]
        public void DynamicDefinitionReadsPropStatically()
        {
            // The read resolves to the prop (static), proving the layout is model-orthogonal.
            const string doc = "@% <dyn(label: string = \"static\")>{{[@(label)]}} :: dynamic %@\n@dyn(this)";
            var t = Compile(doc, ExType.Dynamic);
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());

            dynamic bag = new ExpandoObject();
            bag.other = "x";
            Assert.Equal("[static]", ((string) t.Generate(bag)).Trim());
        }
    }
}
