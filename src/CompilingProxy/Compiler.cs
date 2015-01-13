using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.CSharp;
using Templates.Core.Data;
using Templates.Helpers;
using Templates.Runtime;

namespace CompilingProxy {
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

            foreach (CodeTypeMember member in type.Members) {
                if (member.GetType().IsType<CodeMemberMethod>()) {
                    MethodInfo method = GetMethodReference(results.CompiledAssembly, ns.Name + "." + type.Name, member.Name);
                    member.UserData[StoredDataType.Method] = CompileMethodReference(method);
                }
            }
            return results;
        }

        public Dictionary<string, Type> GetExtensions (string assemblyName)
        {
            Assembly assembly = Assembly.LoadFrom(assemblyName);
            return TemplateFactory.LoadTemplates(assembly);
        }

        private MethodInfo GetMethodReference (Assembly assembly, string typeName, string methodName)
        {
            return assembly.GetType(typeName).GetMethod(methodName);
        }

        private void PushParameter (ILGenerator il, Type parameterType)
        {
            il.Emit(OpCodes.Ldarg_0);
            if (parameterType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, parameterType);
            else if (parameterType != typeof (object))
                il.Emit(OpCodes.Castclass, parameterType);
        }

        private DynamicMethodGateDelegate CompileMethodReference (MethodInfo method)
        {
            var dynamic = new DynamicMethod
                (method.Name, typeof (object), new[]
                {
                    typeof (object), typeof (object)
                }, typeof (Compiler), true);
            ILGenerator il = dynamic.GetILGenerator();
            ParameterInfo[] parameters = method.GetParameters();
            ParameterInfo firstParameter = null;
            ParameterInfo secondParameter = null;
            if (parameters.Any())
                firstParameter = parameters[0];
            if (parameters.Count() > 1)
                secondParameter = parameters[1];
            if (firstParameter != null) {
                PushParameter(il, firstParameter.ParameterType);
                if (secondParameter != null)
                    PushParameter(il, secondParameter.ParameterType);
            }
            il.Emit(OpCodes.Call, method);
            if (method.ReturnType.IsValueType)
                il.Emit(OpCodes.Box, method.ReturnType);
            il.Emit(OpCodes.Ret);
            return (DynamicMethodGateDelegate) dynamic.CreateDelegate(typeof (DynamicMethodGateDelegate));
        }
    }
}