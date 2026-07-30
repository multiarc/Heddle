using System.Collections.Generic;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>
    /// Host extensions that declare an accepted data type. Every <c>[DataType]</c>-declaring extension the repo
    /// had was an engine built-in whose declared type happens to match its call sites exactly — <c>IEnumerable</c>
    /// for <c>@list</c>, <c>Range</c>/<c>int</c> for <c>@for</c> — so nothing measured what the acceptance rule
    /// does when the declared type and the value's type are merely <em>related</em>: a nullable, a covariant
    /// generic interface, a covariant array.
    /// <para>Each renders a fixed marker, so a test reads the same bytes out of both tiers whatever the value was;
    /// what the marker proves is that the call reached the extension at all.</para>
    /// </summary>
    [ExtensionName("takesni")]
    [DataType(typeof(int?))]
    public sealed class TakesNullableIntExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "ni";

        public override void RenderData(in Scope scope) => scope.Renderer.Render("ni");
    }

    /// <summary>Accepts <c>IEnumerable&lt;object&gt;</c> — reached by any sequence of a reference type through
    /// generic covariance, and by an array of one through both covariance and the array's own interface set.</summary>
    [ExtensionName("takesseqobj")]
    [DataType(typeof(IEnumerable<object>))]
    public sealed class TakesObjectSequenceExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "so";

        public override void RenderData(in Scope scope) => scope.Renderer.Render("so");
    }

    /// <summary>Accepts <c>object[]</c> — reached by an array of any reference type through array covariance.</summary>
    [ExtensionName("takesobjarr")]
    [DataType(typeof(object[]))]
    public sealed class TakesObjectArrayExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "oa";

        public override void RenderData(in Scope scope) => scope.Renderer.Render("oa");
    }

    /// <summary>The declaration half of the inheritance rule: the runtime reads <c>[DataType]</c> with
    /// <c>inherit: true</c>, so this base's <c>string</c> travels to every extension derived from it. Carries no
    /// <c>[ExtensionName]</c>, so it is a declaration and not a registered extension.</summary>
    [DataType(typeof(string))]
    public abstract class AcceptsStringExtensionBase : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => "si";

        public override void RenderData(in Scope scope) => scope.Renderer.Render("si");
    }

    /// <summary>Declares <c>int</c> and inherits <c>string</c>: both are accepted, and a type that is neither is
    /// refused. A rule that read only this type's own attributes would take the <c>string</c> away.</summary>
    [ExtensionName("takesstrorint")]
    [DataType(typeof(int))]
    public sealed class TakesStringOrIntExtension : AcceptsStringExtensionBase
    {
    }
}
