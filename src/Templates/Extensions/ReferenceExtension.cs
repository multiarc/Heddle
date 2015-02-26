//using System;
//using Templates.Attributes;
//using Templates.Runtime;

//namespace Templates.Extensions {
//    [Name ("reference")]
//    public class ReferenceExtension: AbstractHtmlExtension {
//        public override Type InitStart (string parameterTemplate, Type dataType, CompileContext context, ParseContext parseContext)
//        {
//            if (context == null)
//                throw new ArgumentNullException("context");

//            if (!string.IsNullOrWhiteSpace(parameterTemplate))
//                context.AddReference(parameterTemplate);
//            return null;
//        }
//    }
//}