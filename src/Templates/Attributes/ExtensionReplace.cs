using System;

namespace Templates.Attributes
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ExtensionReplaceAttribute : Attribute
    {
    }
}