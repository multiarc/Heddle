using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Heddle.Attributes {
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class ExportExtensionsAttribute: Attribute {
        private readonly Type[] _extensions;
        public bool All { get; }

        public IReadOnlyCollection<Type> Extensions => _extensions;

        // The scan-all form is read by enumerating the assembly's types, which a trimmed publish
        // empties; the consumer gets ILLink's warning here instead of silently losing its extensions. The
        // typed forms root their extensions through the constructor parameter annotation.
        [RequiresUnreferencedCode("[ExportExtensions] without a type enumerates the assembly's types at registration, which trimming removes; name the extension types explicitly ([ExportExtensions(typeof(...))]) in a trimmed host.")]
        public ExportExtensionsAttribute()
        {
            All = true;
        }

        // The typeof here roots the extension through a trimmed publish; CreateExtension instantiates it
        // with Activator.CreateInstance, so the annotation keeps the parameterless constructor.
        public ExportExtensionsAttribute(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type extension)
        {
            _extensions = new[] {extension};
            All = false;
        }

        public ExportExtensionsAttribute(params Type[] extensions)
        {
            _extensions = extensions;
            All = false;
        }
    }
}
