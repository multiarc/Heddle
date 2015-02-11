using System;
using Templates.Attributes;
using Templates.Helpers;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("inner")]
    [Name ("describe")]
    [DataType (typeof (object))]
    public class InnerExtension: AbstractExtension {
        public override Type InitializeInnerTemplate(string parameter, System.Type dataType, System.Type additionalType, DocumentContext context)
        {
            return base.InitializeInnerTemplate(parameter, null, dataType, context);
        }

        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            return GetInnerResult(value);
        }
    }
}