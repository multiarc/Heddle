using System;
using System.CodeDom;
using System.Linq;
using System.Text.RegularExpressions;
using Templates.Attributes;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Runtime;

namespace Templates.Extensions {
    [Name ("method")]
    [Name ("call")]
    [Type (typeof (object))]
    [AdditionalType (typeof (object))]
    public class MethodExtension: AbstractExtension {
        private static readonly Regex CodeParseExpression = new Regex
            (@"^\s*(?<return_type>(?<main_type>(@?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+?((\.)|(\+))?)+)(<(?<generic_parameters>((@?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+?((\.)|(\+))?)+(,)?)+)>)?)[\s\n\r\t]*\((?<data>[a-zA-Z_0-9]+)?\)[\s\n\r\t]*(,[\s\n\r\t]*(?<additional>[a-zA-Z_0-9]+))?[\s\n\r\t]*\{(?<code>.*)\}[\s\n\r\t]*$",
             RegexOptions.Singleline | RegexOptions.Compiled);

        private CodeMemberMethod _method;

        protected override object ProcessDataInternal (object value, object additionalValue)
        {
            if (_method != null && _method.UserData[StoredDataType.Method] != null)
            {
                var caller = (DynamicMethodGateDelegate) _method.UserData[StoredDataType.Method];
                return caller(value, additionalValue);
            }
            throw new TemplateProcessingException("Cannot find compiled method reference");
        }

        public override Type InitializeInnerTemplate (string parameter, Type dataType, Type additionalType, CompileContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");
            Match match = CodeParseExpression.Match(parameter);
            if (!match.Success)
                throw new TemplateCompileException
                    ("C# code not wraped up correctly, please see documentation. (ReturnType([data[,additionalData]]) { })");
            _method = context.GetNewMethod();
            if (match.Groups["data"].Value != string.Empty)
                _method.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference(dataType), match.Groups["data"].Value));
            if (match.Groups["additional"].Value != string.Empty)
                _method.Parameters.Add
                    (new CodeParameterDeclarationExpression(new CodeTypeReference(additionalType), match.Groups["additional"].Value));
            _method.ReturnType = new CodeTypeReference(ReflectionHelper.ResolveType(match.Groups["return_type"].Value, context.Namespaces.ToArray()));
            _method.Statements.Add(new CodeSnippetStatement(match.Groups["code"].Value));
            return ReflectionHelper.ResolveType(_method.ReturnType.BaseType, context.Namespaces.ToArray());
        }
    }
}