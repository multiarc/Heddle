using System;
using System.IO;
using System.Reflection;
#if DNXCORE50 || DNX451
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Infrastructure;
#endif

namespace Templates.Data {
    public class TemplateOptions: IEquatable<TemplateOptions> {
#if DNXCORE50 || DNX451
        private static readonly IApplicationEnvironment Environment = 
            (IApplicationEnvironment)CallContextServiceLocator.Locator.ServiceProvider.GetService(typeof(IApplicationEnvironment));
#endif
        public string FileNamePostfix { get; set; }
        public string RootPath { get; set; }
        public string TemplateName { get; }
        public bool EnableFileChangeCheck { get; set; }
        public bool AllowCSharp { get; set; }

        public TemplateOptions()
        {
            FileNamePostfix = string.Empty;
#if DNX451 || DNXCORE50
            RootPath = Environment.ApplicationBasePath;
#else
            RootPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
#endif
            TemplateName = string.Empty;
            EnableFileChangeCheck = false;
            AllowCSharp = false;
        }

        public TemplateOptions(string templateName) {
            FileNamePostfix = string.Empty;
#if DNX451 || DNXCORE50
            RootPath = Environment.ApplicationBasePath;
#else
            RootPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);
#endif
            TemplateName = templateName ?? string.Empty;
            EnableFileChangeCheck = false;
            AllowCSharp = false;
        }

        public TemplateOptions(string fileNamePostfix, string rootPath, string templateName, bool enableFileChangeCheck = false, bool allowCSharp = false)
        {
            if (rootPath == null) throw new ArgumentNullException(nameof(rootPath));
            RootPath = rootPath;
            FileNamePostfix = fileNamePostfix ?? string.Empty;
            TemplateName = templateName ?? string.Empty;
            EnableFileChangeCheck = enableFileChangeCheck;
            AllowCSharp = allowCSharp;
        }

        public TemplateOptions(TemplateOptions value)
        {
            if (value.RootPath == null)
                throw new ArgumentException();
            FileNamePostfix = value.FileNamePostfix;
            RootPath = value.RootPath;
            TemplateName = value.TemplateName;
            EnableFileChangeCheck = value.EnableFileChangeCheck;
            AllowCSharp = value.AllowCSharp;
        }

        public string FullPath => RootPath + TemplateName + FileNamePostfix;

        #region IEquatable<TemplateOptions> Members

        public bool Equals (TemplateOptions other)
        {
            return other.FileNamePostfix == FileNamePostfix && other.TemplateName == TemplateName && other.RootPath == RootPath;
        }

#endregion

        public static bool operator == (TemplateOptions value1, TemplateOptions value2)
        {
            return Equals(value1, value2);
        }

        public static bool operator != (TemplateOptions value1, TemplateOptions value2)
        {
            return !Equals(value1, value2);
        }

        public override bool Equals (object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (!(obj is TemplateOptions))
                return false;
            return Equals((TemplateOptions) obj, this);
        }

        public override int GetHashCode ()
        {
            unchecked {
                return TemplateName?.GetHashCode() ?? 0;
            }
        }
    }
}