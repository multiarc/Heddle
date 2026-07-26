using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Language.Binding;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// <para>Named functions callable from native expressions. Instances are mutable until frozen (first
    /// compile use); frozen instances are immutable and safe for concurrent compiles and renders.</para>
    /// <para>Registration is the host's trust boundary: anything registered is callable from template text.
    /// Names are ordinal and case-sensitive, matching extension lookup.</para>
    /// </summary>
    public sealed class FunctionRegistry
    {
        private readonly Dictionary<string, List<FunctionEntry>> _functions;
        private volatile bool _frozen;

        private static readonly FunctionRegistry DefaultInstance = CreateDefault();

        /// <summary>
        /// The default whitelist (<c>upper, lower, trim, len, contains, startswith, endswith, replace,
        /// substr, format, str, abs, min, max, round, floor, ceil</c> — invariant culture, exception-safe —
        /// plus <c>range(start, last[, step])</c> returning a <see cref="Heddle.Models.Range"/> for counted
        /// <c>@for</c> loops). Instances are immutable once frozen; the shipped set is stable, growing only
        /// additively between phases.
        /// </summary>
        public static FunctionRegistry Default => DefaultInstance;

        private FunctionRegistry(bool seedBuiltIns)
        {
            _functions = new Dictionary<string, List<FunctionEntry>>(StringComparer.Ordinal);
            if (seedBuiltIns)
            {
                foreach (var entry in BuiltInFunctions.CreateEntries())
                    AddOrReplace(entry);
            }
        }

        /// <summary>Creates a registry pre-populated with the <see cref="Default"/> built-ins.</summary>
        public FunctionRegistry()
        {
            _functions = new Dictionary<string, List<FunctionEntry>>(StringComparer.Ordinal);
            foreach (var pair in DefaultInstance._functions)
                _functions[pair.Key] = new List<FunctionEntry>(pair.Value);
        }

        private static FunctionRegistry CreateDefault()
        {
            var registry = new FunctionRegistry(seedBuiltIns: true);
            registry._frozen = true;
            return registry;
        }

        /// <summary>True once the registry has been used by a compile.</summary>
        public bool IsFrozen => _frozen;

        /// <summary>
        /// Registers a delegate (closures allowed). Same name + identical parameter types replaces; otherwise
        /// adds an overload. Throws <see cref="InvalidOperationException"/> when frozen;
        /// <see cref="ArgumentNullException"/> on nulls.
        /// </summary>
        public void Register(string name, Delegate function)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            EnsureMutable();
            AddOrReplace(FunctionEntry.FromDelegate(name, function));
        }

        /// <summary>
        /// Registers a static method. Throws <see cref="ArgumentException"/> for instance methods, open
        /// generics, void returns, or ref/out/pointer parameters.
        /// </summary>
        public void Register(string name, MethodInfo staticMethod)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            if (staticMethod == null)
                throw new ArgumentNullException(nameof(staticMethod));
            // Phase 3 (F2): the four eligibility rejections are the shared ExportRules predicate — the same one the
            // generator now applies, so its manifest overload counts can no longer include a method this method
            // refuses. The throws (and their exact messages) are unchanged.
            var rejection = ExportRules.Evaluate(DescribeMethod(staticMethod));
            if (rejection != ExportRejection.None)
                throw new ArgumentException(ExportRules.RejectionMessage(rejection), nameof(staticMethod));

            EnsureMutable();
            AddOrReplace(FunctionEntry.FromMethod(name, staticMethod));
        }

        /// <summary>True when a function with this exact name is registered.</summary>
        public bool Contains(string name)
        {
            return name != null && _functions.ContainsKey(name);
        }

        /// <summary>
        /// <para>Registers every function the assembly exports via <c>[assembly: ExportFunctions(...)]</c> (phase 6
        /// D24): for each container, each eligible public static method under its lowercase-invariant name, through
        /// the exact <see cref="Register(string, MethodInfo)"/> path (replace-on-exact-signature, overloads ranked
        /// per phase 1 D12).</para>
        /// <para>Throws <see cref="InvalidOperationException"/> when frozen and <see cref="ArgumentException"/> for
        /// an invalid export (non-public/non-static container, or an ineligible method — named in the message).
        /// Idempotent per assembly; not thread-safe pre-freeze (same rule as <see cref="Register(string, MethodInfo)"/>).</para>
        /// </summary>
        public void RegisterFrom(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            foreach (var attribute in assembly.GetCustomAttributes<Heddle.Attributes.ExportFunctionsAttribute>())
            {
                if (attribute?.Containers == null)
                    continue;
                foreach (var container in attribute.Containers)
                    RegisterContainer(container);
            }
        }

        internal void RegisterContainer(Type container)
        {
            if (container == null)
                throw new ArgumentException("An [ExportFunctions] container type is null.");

            bool isStaticClass = container.IsClass && container.IsAbstract && container.IsSealed;
            bool isPublic = container.IsPublic || container.IsNestedPublic;
            if (!ExportRules.IsContainerEligible(isStaticClass, isPublic))
                throw new ArgumentException(ExportRules.ContainerIneligibleMessage(container.FullName));

            foreach (var method in container.GetMethods(BindingFlags.Public | BindingFlags.Static |
                                                        BindingFlags.DeclaredOnly))
            {
                if (!ExportRules.IsCandidate(DescribeMethod(method)))
                    continue; // operators / property accessors are not exportable functions

                var name = ExportRules.FunctionName(method.Name);
                try
                {
                    Register(name, method);
                }
                catch (ArgumentException e)
                {
                    throw new ArgumentException(
                        ExportRules.MethodIneligibleMessage(container.FullName, method.Name,
                            ExportRules.Evaluate(DescribeMethod(method))),
                        e);
                }
            }
        }

        /// <summary>Phase 3 (F2): the shared eligibility record for one reflected method — the reflection adapter of
        /// <see cref="ExportedMethodFacts"/>. <c>ParameterTypeKeys</c> uses <c>Type.FullName</c>, the same spelling
        /// the symbol side produces, so signature identity means the same thing on both tiers.</summary>
        internal static ExportedMethodFacts DescribeMethod(MethodInfo method)
        {
            var parameters = method.GetParameters();
            var keys = new string[parameters.Length];
            bool byRefOrPointer = false;
            for (int i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                if (type.IsByRef || type.IsPointer)
                    byRefOrPointer = true;
                keys[i] = type.FullName ?? type.Name;
            }

            return new ExportedMethodFacts
            {
                Name = method.Name,
                IsStatic = method.IsStatic,
                IsPublic = method.IsPublic,
                IsOpenGeneric = method.ContainsGenericParameters,
                ReturnsVoid = method.ReturnType == typeof(void),
                HasByRefOrPointerParameter = byRefOrPointer,
                IsSpecialName = method.IsSpecialName,
                ParameterTypeKeys = keys
            };
        }

        /// <summary>
        /// Enumerates every registered overload as <c>(Name, Method, ParameterTypes, ReturnType)</c> (phase 6 D3;
        /// feeds LSP completion and hover). <c>Method</c> is the static <see cref="MethodInfo"/> for method
        /// registrations (incl. built-ins) and the delegate's target method for delegate registrations;
        /// <c>ParameterTypes</c>/<c>ReturnType</c> are always populated.
        /// </summary>
        internal IEnumerable<(string Name, MethodInfo Method, Type[] ParameterTypes, Type ReturnType)>
            EnumerateOverloads()
        {
            foreach (var pair in _functions)
            {
                foreach (var entry in pair.Value)
                    yield return (entry.Name, entry.Method ?? entry.Target?.Method, entry.ParameterTypes,
                        entry.ReturnType);
            }
        }

        private void EnsureMutable()
        {
            if (_frozen)
                throw new InvalidOperationException(
                    "This FunctionRegistry has been used by a compile and is now frozen. Register functions before the first compile.");
        }

        private void AddOrReplace(FunctionEntry entry)
        {
            if (!_functions.TryGetValue(entry.Name, out var overloads))
            {
                overloads = new List<FunctionEntry>();
                _functions[entry.Name] = overloads;
            }

            for (int i = 0; i < overloads.Count; i++)
            {
                if (overloads[i].SameSignature(entry))
                {
                    overloads[i] = entry;
                    return;
                }
            }

            overloads.Add(entry);
        }

        /// <summary>Freezes the registry so concurrent compiles and renders read it lock-free. Idempotent.</summary>
        internal void Freeze()
        {
            _frozen = true;
        }

        /// <summary>All overloads registered under <paramref name="name"/>, or an empty list when none.</summary>
        internal IReadOnlyList<FunctionEntry> GetOverloads(string name)
        {
            if (name != null && _functions.TryGetValue(name, out var overloads))
                return overloads;
            return Array.Empty<FunctionEntry>();
        }
    }
}
