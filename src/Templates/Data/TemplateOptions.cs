using System;
using System.IO;

namespace Templates.Data {
    public struct TemplateOptions: IEquatable<TemplateOptions> {
        public string FileNamePostfix;
        public string RootPath;
        public readonly string TemplateName;
        public bool EnableFileChangeCheck;
        public bool AllowCSharp;

        public TemplateOptions()
        {
            FileNamePostfix = string.Empty;
            RootPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            TemplateName = string.Empty;
            EnableFileChangeCheck = false;
            AllowCSharp = false;
        }

        public TemplateOptions(string templateName) {
            FileNamePostfix = string.Empty;
            RootPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().CodeBase);
            TemplateName = templateName ?? string.Empty;
            EnableFileChangeCheck = false;
            AllowCSharp = false;
        }

        public TemplateOptions(string fileNamePostfix, string rootPath, string templateName, bool enableFileChangeCheck = false, bool allowCSharp = false)
        {
            if (rootPath == null) throw new ArgumentNullException("rootPath");
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