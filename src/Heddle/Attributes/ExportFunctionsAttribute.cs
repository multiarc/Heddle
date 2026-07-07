using System;
using System.Collections.Generic;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Assembly-level declarative function export (phase 6 D24). Each container is a
    /// <c>public static</c> class whose eligible public static methods become registrable functions under their
    /// lowercase-invariant method names.</para>
    /// <para>Read by <see cref="Heddle.Runtime.Expressions.FunctionRegistry.RegisterFrom"/> (runtime), the phase 6
    /// workspace scan (editor), and — phase 7 — the source generator (build): one attribute, three readers.
    /// Deliberately mirrors <see cref="ExportExtensionsAttribute"/>'s shape, but has <b>no</b> parameterless
    /// "all" form: function containers carry no structural marker, so an assembly-wide sweep is undefined.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class ExportFunctionsAttribute : Attribute
    {
        private readonly Type[] _containers;

        /// <summary>Exports one container class.</summary>
        public ExportFunctionsAttribute(Type container)
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
