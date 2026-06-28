using Microsoft.DotNet.PlatformAbstractions;
using System;

namespace Templates.Data {
    public class TemplateOptions: IEquatable<TemplateOptions>
    {
        public string FileNamePostfix { get; set; }
        public string RootPath { get; set; }
        public string TemplateName { get; }
        public bool EnableFileChangeCheck { get; set; }
        public bool AllowCSharp { get; set; }
        public int MaxRecursionCount { get; set; }
        public bool ProvideLanguageFeatures { get; set; }
        public object Data { get; set; }

        public TemplateOptions()
        {
            FileNamePostfix = string.Empty;
            RootPath = AppContext.BaseDirectory;
            TemplateName = string.Empty;
            EnableFileChangeCheck = false;
            AllowCSharp = false;
            MaxRecursionCount = 100;
        }

        public TemplateOptions(string templateName) {
            FileNamePostfix = string.Empty;
            RootPath = AppContext.BaseDirectory;
            TemplateName = templateName ?? string.Empty;
            EnableFileChangeCheck = false;
            AllowCSharp = false;
            MaxRecursionCount = 100;
        }
        
        public TemplateOptions(TemplateOptions value, string templateName = null)
        {
            FileNamePostfix = value.FileNamePostfix;
            RootPath = value.RootPath ?? throw new ArgumentException();
            TemplateName = templateName ?? value.TemplateName;
            EnableFileChangeCheck = value.EnableFileChangeCheck;
            AllowCSharp = value.AllowCSharp;
            MaxRecursionCount = value.MaxRecursionCount;
            Data = value.Data;
        }

        public string FullPath => RootPath + TemplateName + FileNamePostfix;

        public bool Equals(TemplateOptions other)
        {
            return other.FileNamePostfix == FileNamePostfix && other.TemplateName == TemplateName && other.RootPath == RootPath;
        }

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
            return Equals((TemplateOptions) obj);
        }

        public override int GetHashCode ()
        {
            unchecked {
                return TemplateName?.GetHashCode() ?? 0;
            }
        }
    }
}