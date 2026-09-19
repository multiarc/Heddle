using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Assembly-level declarative function export. Each container is a
    /// <c>public static</c> class whose eligible public static methods become registrable functions under their
    /// lowercase-invariant method names.</para>
    /// <para>Read by <see cref="Heddle.Runtime.Expressions.FunctionRegistry.RegisterFrom"/> (runtime), the
    /// workspace scan (editor), and the build host (build): one attribute, three readers.
    /// Deliberately mirrors <see cref="ExportExtensionsAttribute"/>'s shape, but has <b>no</b> parameterless
    /// "all" form: function containers carry no structural marker, so an assembly-wide sweep is undefined.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ExportFunctionsAttribute : Attribute
    {
        private readonly Type[] _containers;

        /// <summary>Exports one container class.</summary>
        // The typeof here roots the container through a trimmed publish; RegisterContainer reads its
        // public static methods by reflection, so the annotation keeps them.
        public ExportFunctionsAttribute(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type container)
        {
            _containers = new[] { container };
        }

        /// <summary>Exports several container classes.</summary>
        public ExportFunctionsAttribute(params Type[] containers)
        {
            _containers = containers ?? Array.Empty<Type>();
        }

        /// <summary>The exported container classes, in declaration order.</summary>
        public IReadOnlyCollection<Type> Containers => _containers;
    }
}
