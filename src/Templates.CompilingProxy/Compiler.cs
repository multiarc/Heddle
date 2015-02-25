using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.CSharp;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Runtime;
using Templates.Strings;

namespace Templates.CompilingProxy {
    [Serializable]
    internal class Compiler {
        public CompilerResults Compile (CodeCompileUnit compileUnit, CodeTypeDeclaration type, CodeNamespace ns)
        {
            var codeProvider = new CSharpCodeProvider();

            var parameters = new CompilerParameters
            {
                GenerateExecutable = false,
                IncludeDebugInformation = false,
                GenerateInMemory = true
            };
            CompilerResults results = codeProvider.CompileAssemblyFromDom(parameters, compileUnit);
            if (results.Errors.HasErrors)
            {
                ExStringBuilder textBuilder = new ExStringBuilder();
                foreach (CompilerError error in results.Errors)
                {
                    textBuilder.Append("[{0}]{1}({2})", error.ErrorNumber, error.ErrorText, error.Line);
                }
                throw new TemplateCompileException(textBuilder.ToString());
            }

            foreach (CodeTypeMember member in type.Members) {
                if (member.GetType().IsType<CodeMemberMethod>()) {
                    MethodInfo method = GetMethodReference(results.CompiledAssembly, ns.Name + "." + type.Name, member.Name);
                    member.UserData[StoredDataType.Method] = GatesCache.GetMethodGate(method);
                }
            }
            return results;
        }

        public Dictionary<string, Type> GetExtensions (string assemblyName)
        {
            Assembly assembly = Assembly.LoadFrom(assemblyName);
            return TemplateFactory.LoadExtensions(assembly);
        }

        private MethodInfo GetMethodReference (Assembly assembly, string typeName, string methodName)
        {
            return assembly.GetType(typeName).GetMethod(methodName);
        }
    }
}