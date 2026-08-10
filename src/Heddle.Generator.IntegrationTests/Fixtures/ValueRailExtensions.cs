using System.Globalization;
using Heddle.Attributes;
using Heddle.Core;
using Heddle.Data;

[assembly: ExportExtensions(typeof(Heddle.Generator.IntegrationTests.Fixtures.TallyExtension))]

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>
    /// A host extension that never hands back a string: <c>ProcessData</c> returns a boxed <see cref="int"/> while
    /// <c>RenderData</c> stringifies the same number itself. One box, two paths — the value path's coercion drops it
    /// and the render path keeps it.
    /// </summary>
    [ExtensionName("tally")]
    public sealed class TallyExtension : AbstractExtension
    {
        public override object ProcessData(in Scope scope) => Tally(scope);

        public override void RenderData(in Scope scope) =>
            scope.Renderer.Render(Tally(scope).ToString(CultureInfo.InvariantCulture));

        private static int Tally(in Scope scope) => (scope.ModelData as string)?.Length ?? -1;
    }
}
