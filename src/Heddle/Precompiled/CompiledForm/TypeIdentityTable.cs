using System;
using System.Collections.Generic;
using System.Text;

namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>A type reference carried by the artifact: a named type, a constructed generic, an array,
    /// or the engine's dynamic type. Instances are immutable and compare by structure, so the writer interns
    /// them by identity in first-use order.</summary>
    internal abstract class CompiledTypeRef
    {
        /// <summary>The nominal form used wherever an identity is compared: the CLR full name and the
        /// assembly simple name, joined exactly as the runtime formats a live type.</summary>
        public abstract string Nominal();
    }

    /// <summary>A named type: CLR metadata full name (nested types joined with <c>+</c>, no type arguments),
    /// the assembly simple name, and whether the writer classified the assembly as a framework assembly.</summary>
    internal sealed class NamedTypeRef : CompiledTypeRef
    {
        public NamedTypeRef(string fullName, string assemblySimpleName, bool isFramework)
        {
            if (fullName == null)
                throw new ArgumentNullException(nameof(fullName));
            if (assemblySimpleName == null)
                throw new ArgumentNullException(nameof(assemblySimpleName));
            FullName = fullName;
            AssemblySimpleName = assemblySimpleName;
            IsFramework = isFramework;
        }

        public string FullName { get; }

        public string AssemblySimpleName { get; }

        public bool IsFramework { get; }

        public override string Nominal() => FullName + ", " + AssemblySimpleName;

        public override bool Equals(object obj)
        {
            var other = obj as NamedTypeRef;
            return other != null &&
                other.FullName == FullName &&
                other.AssemblySimpleName == AssemblySimpleName &&
                other.IsFramework == IsFramework;
        }

        public override int GetHashCode()
        {
            int hash = FullName.GetHashCode() ^ AssemblySimpleName.GetHashCode();
            return IsFramework ? hash ^ 0x5F3A9D41 : hash;
        }
    }

    /// <summary>A constructed generic: the generic definition plus its ordered type arguments
    /// (<c>Nullable&lt;T&gt;</c> is the constructed generic it is).</summary>
    internal sealed class GenericTypeRef : CompiledTypeRef
    {
        public GenericTypeRef(NamedTypeRef definition, IReadOnlyList<CompiledTypeRef> arguments)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));
            Definition = definition;
            Arguments = arguments;
        }

        public NamedTypeRef Definition { get; }

        public IReadOnlyList<CompiledTypeRef> Arguments { get; }

        public override string Nominal()
        {
            var inner = new StringBuilder();
            for (int i = 0; i < Arguments.Count; i++)
            {
                if (i != 0)
                    inner.Append(", ");
                inner.Append(Arguments[i] == null ? AqnFormatter.Unknown : Arguments[i].Nominal());
            }
            return Definition.Nominal() + "[" + inner + "]";
        }

        public override bool Equals(object obj)
        {
            var other = obj as GenericTypeRef;
            if (other == null || !other.Definition.Equals(Definition) || other.Arguments.Count != Arguments.Count)
                return false;
            for (int i = 0; i < Arguments.Count; i++)
                if (!Equals(Arguments[i], other.Arguments[i]))
                    return false;
            return true;
        }

        public override int GetHashCode()
        {
            int hash = Definition.GetHashCode();
            for (int i = 0; i < Arguments.Count; i++)
                hash = (hash * 31) + (Arguments[i] == null ? 0 : Arguments[i].GetHashCode());
            return hash;
        }
    }

    /// <summary>An array: the element type plus the rank.</summary>
    internal sealed class ArrayTypeRef : CompiledTypeRef
    {
        public ArrayTypeRef(CompiledTypeRef element, int rank)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));
            Element = element;
            Rank = rank;
        }

        public CompiledTypeRef Element { get; }

        public int Rank { get; }

        public override string Nominal()
        {
            if (Rank <= 1)
                return Element.Nominal() + "[]";
            return Element.Nominal() + "[" + new string(',', Rank - 1) + "]";
        }

        public override bool Equals(object obj)
        {
            var other = obj as ArrayTypeRef;
            return other != null && other.Rank == Rank && Equals(Element, other.Element);
        }

        public override int GetHashCode() => Element.GetHashCode() ^ Rank;
    }

    /// <summary>The engine's dynamic type — a template typed <c>:: dynamic</c>, distinct from
    /// <c>System.Object</c>.</summary>
    internal sealed class DynamicTypeRef : CompiledTypeRef
    {
        public static readonly DynamicTypeRef Instance = new DynamicTypeRef();

        private DynamicTypeRef()
        {
        }

        public override string Nominal() => "dynamic";

        public override bool Equals(object obj) => obj is DynamicTypeRef;

        public override int GetHashCode() => 0x2B0017;
    }

    /// <summary>Interns type references by structural identity in first-use order. One instance serves one
    /// artifact write, so the type table order is a pure function of the walked model.</summary>
    internal sealed class TypeIdentityTable
    {
        private readonly Dictionary<CompiledTypeRef, int> _indexByRef = new Dictionary<CompiledTypeRef, int>();
        private readonly List<CompiledTypeRef> _ordered = new List<CompiledTypeRef>();

        /// <summary>The interned references in first-use order.</summary>
        public IReadOnlyList<CompiledTypeRef> OrderedRefs => _ordered;

        /// <summary>Returns true when <paramref name="typeRef"/> is already interned.</summary>
        public bool TryGetIndex(CompiledTypeRef typeRef, out int index) =>
            _indexByRef.TryGetValue(typeRef, out index);

        /// <summary>Returns the index of <paramref name="typeRef"/>, interning it first when unseen.</summary>
        public int Intern(CompiledTypeRef typeRef)
        {
            if (typeRef == null)
                throw new ArgumentNullException(nameof(typeRef));
            int index;
            if (_indexByRef.TryGetValue(typeRef, out index))
                return index;
            index = _ordered.Count;
            _ordered.Add(typeRef);
            _indexByRef.Add(typeRef, index);
            return index;
        }
    }
}
