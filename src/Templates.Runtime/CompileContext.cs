using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Templates.Data;
using Templates.Exceptions;
using Templates.Helpers;
using Templates.Strings.Core;

namespace Templates.Runtime {
    public class CompileContext: IDisposable {
        private const string TypeName = "Generated";
        private const string ProxyAssembly = "Templates.CompilingProxy, Version=1.0.0.0, Culture=neutral, PublicKeyToken=144ba7f33aad5b85";
        private readonly int _encloseLevel;

        private DataWrapper _data;

        public CompileContext (TemplateOptions options)
        {
            InitNewCompileUnit(options.TemplateName);
            ModelType = typeof (object);
            Options = options;
        }

        public CompileContext (CompileContext context, string newName)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            newName = newName ?? string.Empty;

            InitNewCompileUnit(newName);
            ModelType = typeof (object);
            Options = new TemplateOptions
            {
                FileNamePostfix = context.Options.FileNamePostfix,
                RootPath = context.Options.RootPath,
                TemplateName = newName
            };
        }

        public CompileContext (CompileContext context, Type newType)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            InitNewCompileUnit(context:context);
            Options = context.Options;
            ModelType = newType ?? typeof (object);
            _encloseLevel = context._encloseLevel + 1;
        }

        public CompileContext (CompileContext context, Type newType, string newName)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            newName = newName ?? string.Empty;

            InitNewCompileUnit(newName);
            Options = new TemplateOptions
            {
                FileNamePostfix = context.Options.FileNamePostfix,
                RootPath = context.Options.RootPath,
                TemplateName = newName
            };
            ModelType = newType ?? typeof (object);
        }

        public TemplateOptions Options
        {
            get;
            private set;
        }

        public Type ModelType
        {
            get;
            set;
        }

        public IEnumerable<string> Namespaces
        {
            get
            {
                var list = _data.Ns.Imports.Cast<CodeNamespaceImport>().Select(ns => ns.Namespace).ToList();
                return list.AsReadOnly();
            }
        }

        public IDictionary<string, Type> Extensions
        {
            get { return _data.Extensions; }
        }

        #region IDisposable Members

        public void Dispose ()
        {
            _data.Extensions = null;
            _data.ProxyCompiler = null;
            if (_data.CodeDomain != null) {
                AppDomain.Unload(_data.CodeDomain);
                _data.CodeDomain = null;
            }
            _data.CompileUnit = null;
            _data.Ns = null;
            _data.Type = null;
        }

        #endregion

        private void InitNewCompileUnit (string namespaceName = null, CompileContext context = null)
        {
            if (context == null) {
                _data = new DataWrapper
                {
                    CompileUnit = new CodeCompileUnit(),
                    Ns = new CodeNamespace(namespaceName),
                    Type = new CodeTypeDeclaration(TypeName)
                    {
                        TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed,
                    }
                };

                _data.Ns.Types.Add(_data.Type);
                _data.CompileUnit.Namespaces.Add(_data.Ns);
                ResetContext();
            } else
                _data = context._data;
        }

        public virtual void ResetContext()
        {
            if (_data.ReservedCodeDomain == null) {
                _data.ReservedCodeDomain = _data.CodeDomain;
                _data.ReservedExtensions = _data.Extensions;
                _data.ReservedProxyCompiler = _data.ProxyCompiler;
            }
            _data.Extensions = new Dictionary<string, Type>();
            _data.CodeDomain = AppDomain.CreateDomain(Options.TemplateName ?? "");
            _data.ProxyCompiler = _data.CodeDomain.CreateInstanceAndUnwrap(ProxyAssembly, "Templates.CompilingProxy.Compiler");
        }

        public virtual void RevertBack()
        {
            if (_data.CodeDomain != null) {
                AppDomain.Unload(_data.CodeDomain);
                _data.CodeDomain = null;
            }
            if (_data.ReservedCodeDomain != null) {
                _data.CodeDomain = _data.ReservedCodeDomain;
                _data.Extensions = _data.ReservedExtensions;
                _data.ProxyCompiler = _data.ReservedProxyCompiler;
                _data.ReservedCodeDomain = null;
            }
        }

        public virtual void Commit()
        {
            if (_data.ReservedCodeDomain != null) {
                AppDomain.Unload(_data.ReservedCodeDomain);
                _data.ReservedCodeDomain = null;
            }
            _data.ReservedExtensions = null;
            _data.ReservedProxyCompiler = null;
        }

        public virtual void ImportNamespace (string namespaceName)
        {
            _data.Ns.Imports.Add(new CodeNamespaceImport(namespaceName));
        }

        public virtual void AddReference(string assemblyName)
        {
            _data.CompileUnit.ReferencedAssemblies.Add(assemblyName);
            var helper = new ReflectionHelper(_data.ProxyCompiler.GetType());
            var extensions = helper.Invoke<Dictionary<string, Type>>(_data.ProxyCompiler, "GetExtensions", assemblyName);
            foreach (var extension in extensions)
                _data.Extensions.Add(extension.Key, extension.Value);
        }

        private void AddAutoReferences ()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (Assembly assembly in assemblies) {
                foreach (string namespc in Namespaces) {
                    if (assembly.GetTypes().Any(t => t.Namespace == namespc)) {
                        if (!_data.CompileUnit.ReferencedAssemblies.Contains(assembly.Location)
                            && !_data.CompileUnit.ReferencedAssemblies.Contains(assembly.FullName))
                            _data.CompileUnit.ReferencedAssemblies.Add(assembly.Location);
                    }
                }
            }
        }

        public virtual void Compile()
        {
            foreach (var delayedTemplate in _delayedTemplates) {
                delayedTemplate.ForExtension.ParseInnerTemplate(delayedTemplate.NewContext);
            }
            _delayedTemplates.Clear();
            if (_encloseLevel == 0 && _data.Type.Members.Count > 0) {
                AddAutoReferences();
                var helper = new ReflectionHelper(_data.ProxyCompiler.GetType());
                var results = helper.Invoke<CompilerResults>(_data.ProxyCompiler, "Compile", _data.CompileUnit, _data.Type, _data.Ns);
                if (results.Errors.Count != 0) {
                    var errors = new ExStringBuilder();
                    foreach (CompilerError error in results.Errors) {
                        errors.Append(error.ErrorText);
                        errors.Append("\n");
                    }
                    throw new TemplateCompileException(errors.ToString());
                }
            }
        }

        private class DelayedTemplate
        {
            public CompileContext NewContext;
            public IExtension ForExtension;
        }

        private readonly List<DelayedTemplate> _delayedTemplates = new List<DelayedTemplate>();

        public virtual void AddDelayedCompileTemplate(CompileContext newContext, IExtension forExtension)
        {
            _delayedTemplates.Add(new DelayedTemplate
            {
                NewContext = newContext,
                ForExtension = forExtension
            });
        }

        public virtual CodeMemberMethod GetNewMethod()
        {
            var result = new CodeMemberMethod
            {
                // ReSharper disable BitwiseOperatorOnEnumWihtoutFlags
                Attributes = MemberAttributes.Static | MemberAttributes.Public,
                // ReSharper restore BitwiseOperatorOnEnumWihtoutFlags
                Name = "ParseData_" + _data.MethodCount
            };
            _data.MethodCount++;
            _data.Type.Members.Add(result);
            return result;
        }

        #region Nested type: DataWrapper

        private class DataWrapper {
            public AppDomain CodeDomain;
            public CodeCompileUnit CompileUnit;
            public IDictionary<string, Type> Extensions;
            public int MethodCount;
            public CodeNamespace Ns;
            public object ProxyCompiler;
            public CodeTypeDeclaration Type;

            public AppDomain ReservedCodeDomain;
            public IDictionary<string, Type> ReservedExtensions;
            public object ReservedProxyCompiler;
        }

        #endregion
    }
}