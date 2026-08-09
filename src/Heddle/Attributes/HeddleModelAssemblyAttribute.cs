using System;
using System.Collections.Generic;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Assembly-level declaration that the assemblies the named types come from carry <c>@model</c> types the
    /// engine must be able to resolve. Each <c>typeof(T)</c> names an <b>assembly</b>, not a single type: the whole
    /// assembly becomes visible to engine type resolution, exactly as
    /// <see cref="HeddleTemplate.Register(System.Reflection.Assembly)"/> makes it.</para>
    /// <para><b>Why a <c>typeof</c> rather than a name.</b> A type can only be spelled from a project that
    /// references its assembly, so the reference stops being a documentation rule the host can violate silently
    /// and becomes a fact the C# compiler enforces — a missing one is CS0246 in the host's own source, not a
    /// degrade at first render. The same reference is what the <b>build</b> tier needs: it reaches
    /// <c>@(ReferencePath)</c>, the source generator indexes it with the rest of the compilation's reference
    /// closure, and the same <c>@model</c> spelling binds there too. One declaration, both tiers.</para>
    /// <para>Read by <see cref="HeddleTemplate.Register(System.Reflection.Assembly)"/> (runtime), by the compiler's
    /// reference resolution on behalf of the source generator (build), and — through its own workspace
    /// <c>assemblies</c> setting — by the editor: one contract, three readers. Deliberately mirrors
    /// <see cref="ExportExtensionsAttribute"/>'s shape, but has <b>no</b> parameterless "all" form: an assembly
    /// carries no structural marker saying it holds model types, so an assembly-wide sweep is undefined.</para>
    /// <para>What is declared here is <b>not</b> removable. A workspace registration is tracked so a reload can
    /// drop it and let a collectible load context collect; a statically declared model assembly is neither
    /// collectible nor the thing a reload is trying to let go of, so it stays registered for the process.</para>
    /// <para>Configuration decides <i>whether</i> a template precompiles, never a rendered byte, so it is not part
    /// of the precompiled options fingerprint and adding it changes no generated output.</para>
    /// </summary>
    /// <example>
    /// <code>
    /// [assembly: Heddle.Attributes.HeddleModelAssembly(typeof(Acme.Models.Invoice))]
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class HeddleModelAssemblyAttribute : Attribute
    {
        private readonly Type[] _modelTypes;

        /// <summary>Declares the assembly one type comes from.</summary>
        public HeddleModelAssemblyAttribute(Type modelType)
        {
            _modelTypes = new[] { modelType };
        }

        /// <summary>Declares the assemblies several types come from.</summary>
        public HeddleModelAssemblyAttribute(params Type[] modelTypes)
        {
            _modelTypes = modelTypes ?? Array.Empty<Type>();
        }

        /// <summary>The declared types, in declaration order. Only the assembly each one comes from is used.</summary>
        public IReadOnlyCollection<Type> ModelTypes => _modelTypes;
    }
}
