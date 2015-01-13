using System;
using Templates.Attributes;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("inner")]
    [Name ("describe")]
    [Type (typeof (object))]
    public class InnerExtension: AbstractExtension {
        public override Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            return base.InitializeInnerTemplate(parameter, null, dataType, context);
        }

        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            return GetInnerResult(value);
        }
    }
}