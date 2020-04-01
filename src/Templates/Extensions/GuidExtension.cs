using System;
using Templates.Attributes;
using Templates.Core;
using Templates.Data;

namespace Templates.Extensions
{
    /// <summary>
    /// <para>Guid Template</para>
    /// <para>Optional string represents GUID formatting</para>
    /// </summary>
    [ExtensionName("guid")]
    [DataType(typeof(Guid))]
    public class GuidExtension : AbstractExtension
    {
        public override ExType InitStart(InitContext initContext, ExType dataType, ExType chainedType, ExType parent)
        {
            return base.InitStart(initContext, parent, chainedType, null);
        }

        public override object ProcessData(in Scope scope)
        {
            if (!(scope.ModelData is Guid))
                return string.Empty;
            var parentData = scope.Parent();
            return ((Guid) scope.ModelData).ToString(GetInnerResult(parentData));
        }

        public override void RenderData(in Scope scope)
        {
            if (!(scope.ModelData is Guid guid))
                return;

            var parentData = scope.Parent();
            scope.Renderer.Render(guid.ToString(GetInnerResult(parentData)));
        }
    }
}