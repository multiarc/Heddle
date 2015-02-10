using System;

namespace Templates.Data {
    public struct TemplateOptions: IEquatable<TemplateOptions> {
        public string FileNamePostfix;
        public string RootPath;
        public string TemplateName;
        public bool EnableFileChangeCheck;

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
                // ReSharper disable NonReadonlyFieldInGetHashCode
                int result = (FileNamePostfix != null ? FileNamePostfix.GetHashCode() : 0);
                result = (result * 397) ^ (TemplateName != null ? TemplateName.GetHashCode() : 0);
                result = (result * 397) ^ (RootPath != null ? RootPath.GetHashCode() : 0);
                // ReSharper restore NonReadonlyFieldInGetHashCode
                return result;
            }
        }
    }
}