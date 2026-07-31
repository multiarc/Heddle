using System;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>A host extension this assembly exports and the generated assembly may not name: reflection
    /// instantiates a non-public type with a public constructor without comment, so the engine registers it and
    /// renders, while a <c>new</c> expression spelling the same name in the consumer's own compilation is CS0122.
    /// </summary>
    [ExtensionName("secret")]
    internal sealed class SecretExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) =>
            "<" + (scope.ModelData?.ToString() ?? string.Empty) + ">";

        public override void RenderData(in Scope scope) =>
            scope.Renderer.Render((string) ProcessData(scope));
    }

    /// <summary>The same inaccessibility on the parameter-declaring path, which allocates its field through a
    /// different writer and so is a second emission site rather than a second spelling of the first.</summary>
    [ExtensionName("boxed")]
    [Prop("width", typeof(int), Default = 5)]
    internal sealed class BoxedExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) =>
            "{" + scope.GetParameter("width") + ":" + (scope.ModelData?.ToString() ?? string.Empty) + "}";

        public override void RenderData(in Scope scope) =>
            scope.Renderer.Render((string) ProcessData(scope));
    }

    /// <summary>Deprecation at warning level. The consumer's build reports it and carries on, so the name stays
    /// writable and the template must keep pre-compiling — the refusal is about names the compiler <i>rejects</i>,
    /// not about names it comments on.</summary>
    [ExtensionName("aging")]
    [Obsolete("prefer @yell")]
    public sealed class AgingExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) =>
            "(" + (scope.ModelData?.ToString() ?? string.Empty) + ")";

        public override void RenderData(in Scope scope) =>
            scope.Renderer.Render((string) ProcessData(scope));
    }
}
