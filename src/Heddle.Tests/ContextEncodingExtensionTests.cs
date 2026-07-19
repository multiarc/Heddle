using System.Reflection;
using Heddle;
using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// C2-R7 profile-interplay and value-call semantics for <c>@attr</c>/<c>@js</c>/<c>@url</c> rendered end-to-end
    /// through the engine: under <see cref="OutputProfile.Html"/> the encoding leaves (<c>@attr</c>, <c>@url</c>) are
    /// not double-encoded and the raw leaf (<c>@js</c>) is not entity-encoded; a <c>null</c> value renders the
    /// body-as-default in the caller's (parent) scope, or empty when there is no body; a non-string value is
    /// stringified before escaping.
    /// </summary>
    public class ContextEncodingExtensionTests
    {
        public class Model
        {
            public string Value { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
        }

        private static string Render(string template, object model, OutputProfile profile = OutputProfile.Html)
        {
            HeddleTemplate.Configure(typeof(ContextEncodingExtensionTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions { OutputProfile = profile }, model.GetType()));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        // A dynamic model reaches the runtime stringify path: like @string, a statically non-string parameter is a
        // compile-time type error (HED0004); "any object" input flows through the dynamic tier.
        private static string RenderDynamic(string template, object model, OutputProfile profile = OutputProfile.Html)
        {
            HeddleTemplate.Configure(typeof(ContextEncodingExtensionTests).GetTypeInfo().Assembly);
            var t = new HeddleTemplate(template,
                new CompileContext(new TemplateOptions { OutputProfile = profile }, ExType.Dynamic));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            return t.Generate(model);
        }

        [Fact]
        public void Attr_Html_NotDoubleEncoded()
        {
            var actual = Render("<a title=\"@attr(Value)\">", new Model { Value = "a&b<c>\"d'e" });
            // @attr's own escaping (incl. ' and ") only — no double &amp;amp;.
            Assert.Equal("<a title=\"a&amp;b&lt;c&gt;&quot;d&#39;e\">", actual);
        }

        [Fact]
        public void Url_Html_NotDoubleEncoded()
        {
            var actual = Render("<a href=\"?q=@url(Value)\">", new Model { Value = "a b&c" });
            Assert.Equal("<a href=\"?q=a%20b%26c\">", actual); // %26 not %26amp;
        }

        [Fact]
        public void Js_Html_NotEntityEncoded()
        {
            var actual = Render("<script>var x = \"@js(Value)\";</script>",
                new Model { Value = "</script>&\"y" });
            // JS escaping, no HTML entities (& stays &, not &amp;).
            Assert.Equal("<script>var x = \"\\u003C/script>\\u0026\\\"y\";</script>", actual);
        }

        [Fact]
        public void Text_Profile_SameEscaping()
        {
            // The context encoders are named calls (not the unnamed carrier), so their output is identical under Text.
            var actual = Render("<a title=\"@attr(Value)\">", new Model { Value = "a&b" }, OutputProfile.Text);
            Assert.Equal("<a title=\"a&amp;b\">", actual);
        }

        [Fact]
        public void NullModel_WithLiteralBody_RendersDefault()
        {
            var actual = Render("[@attr(Value){{fallback}}]", new Model { Value = null });
            Assert.Equal("[fallback]", actual);
        }

        [Fact]
        public void NullModel_WithBody_RendersInParentScope()
        {
            // The default body steps back to the caller's scope, so @(Name) resolves the sibling property (mirrors
            // @string). Under Html the unnamed carrier encodes Name — proving both step-back and profile.
            var actual = Render("@attr(Value){{@(Name)}}", new Model { Value = null, Name = "Jo&e" });
            Assert.Equal("Jo&amp;e", actual);
        }

        [Fact]
        public void NullModel_NoBody_RendersEmpty()
        {
            Assert.Equal("[]", Render("[@attr(Value)]", new Model { Value = null }));
            Assert.Equal("[]", Render("[@js(Value)]", new Model { Value = null }));
            Assert.Equal("[]", Render("[@url(Value)]", new Model { Value = null }));
        }

        [Fact]
        public void NonStringModel_Stringified()
        {
            Assert.Equal("42", RenderDynamic("@attr(Count)", new Model { Count = 42 }));
            Assert.Equal("42", RenderDynamic("@js(Count)", new Model { Count = 42 }));
            Assert.Equal("42", RenderDynamic("@url(Count)", new Model { Count = 42 }));
        }
    }
}
