using System.Collections.Generic;

namespace Heddle.Language.Binding
{
    /// <summary>Why the runtime refuses to register an exported method. <see cref="None"/> means eligible.</summary>
    internal enum ExportRejection
    {
        None = 0,
        NotStatic,
        OpenGeneric,
        ReturnsVoid,
        ByRefOrPointerParameter
    }

    /// <summary>
    /// The facts the shared export rule-core evaluates — deliberately a pure record with no type
    /// system in it, so neither side re-transcribes the eligibility predicate.
    /// </summary>
    internal struct ExportedMethodFacts
    {
        /// <summary>The declared (cased) method name.</summary>
        public string Name;

        public bool IsStatic;
        public bool IsPublic;
        public bool IsOpenGeneric;
        public bool ReturnsVoid;
        public bool HasByRefOrPointerParameter;

        /// <summary>Reflection's <c>MethodInfo.IsSpecialName</c>. The Roslyn adapter maps
        /// <c>MethodKind != Ordinary</c> onto this — a close but not identical relation, documented at the adapter
        /// (a user-defined operator is <c>MethodKind.UserDefinedOperator</c> <i>and</i> special-name, but an
        /// ordinary method can never be special-name, so the mapping is conservative in the safe direction).</summary>
        public bool IsSpecialName;

        /// <summary>Ordered parameter-type keys, in whatever spelling the adapter uses for signature identity
        /// (reflection: <c>Type.FullName</c>; Roslyn: the same, through the shared display format). Two
        /// registrations with equal keys are the same signature and <b>replace</b> rather than add an overload.</summary>
        public IReadOnlyList<string> ParameterTypeKeys;
    }

    /// <summary>
    /// The <c>[ExportFunctions]</c> eligibility, naming and merge rules, stated once.
    /// <para>Runtime authority is <c>FunctionRegistry.RegisterContainer</c>/<c>Register</c>/<c>AddOrReplace</c>. The
    /// generator's transcription silently skipped ineligible containers, counted methods the runtime <em>refuses</em>
    /// (its per-function overload count included <c>void</c>, open-generic and by-ref methods), and gave a function
    /// name exclusively to the first container that claimed it. Because the gauntlet compares overload counts
    /// <b>exactly</b>, one <c>void Log(string)</c> helper in an export container permanently un-precompiled every
    /// template calling any function from it.</para>
    /// </summary>
    internal static class ExportRules
    {
        /// <summary>Container eligibility: a public static class. Reflection spells "static class" as
        /// <c>IsClass &amp;&amp; IsAbstract &amp;&amp; IsSealed</c> and "public" as
        /// <c>IsPublic || IsNestedPublic</c> — a <b>nested</b> public container is legal, which the symbol side
        /// must accept too.</summary>
        internal static bool IsContainerEligible(bool isStaticClass, bool isPublicOrNestedPublic) =>
            isStaticClass && isPublicOrNestedPublic;

        /// <summary>The declared-only, public, static, non-special-name filter the runtime's
        /// <c>BindingFlags.Public | Static | DeclaredOnly</c> enumeration plus its <c>IsSpecialName</c>
        /// <c>continue</c> apply. A skipped method is <b>not</b> an error — it is simply not a function.</summary>
        internal static bool IsCandidate(in ExportedMethodFacts facts) =>
            facts.IsPublic && facts.IsStatic && !facts.IsSpecialName;

        /// <summary>Per-method eligibility. The runtime raises <c>ArgumentException</c> for each of these; the
        /// generator must exclude exactly the same methods from its manifest counts, which alone fixes the standing
        /// overload-count gauntlet failure.</summary>
        internal static ExportRejection Evaluate(in ExportedMethodFacts facts)
        {
            if (!facts.IsStatic)
                return ExportRejection.NotStatic;
            if (facts.IsOpenGeneric)
                return ExportRejection.OpenGeneric;
            if (facts.ReturnsVoid)
                return ExportRejection.ReturnsVoid;
            if (facts.HasByRefOrPointerParameter)
                return ExportRejection.ByRefOrPointerParameter;
            return ExportRejection.None;
        }

        /// <summary>The runtime's <c>Register(string, MethodInfo)</c> messages, so the build tier's diagnostic can
        /// quote the same reason the run tier's exception does.</summary>
        internal static string RejectionMessage(ExportRejection rejection)
        {
            switch (rejection)
            {
                case ExportRejection.NotStatic:
                    return "A registered function method must be static.";
                case ExportRejection.OpenGeneric:
                    return "A registered function method must not be an open generic.";
                case ExportRejection.ReturnsVoid:
                    return "A registered function method must return a value.";
                case ExportRejection.ByRefOrPointerParameter:
                    return "A registered function method must not have ref/out/pointer parameters.";
                default:
                    return null;
            }
        }

        /// <summary>The function name a method registers under: <c>Name.ToLowerInvariant()</c>. Trivial, but stated
        /// once so a future normalization change cannot fork.</summary>
        internal static string FunctionName(string methodName) => methodName?.ToLowerInvariant();

        /// <summary>The runtime's <c>RegisterContainer</c> wrapper around a rejected method — the message an
        /// <c>[ExportFunctions]</c> host sees at startup, and the one the build tier's <c>HED7021</c> quotes for the
        /// same method.</summary>
        internal static string MethodIneligibleMessage(string containerFullName, string methodName,
            ExportRejection rejection) =>
            "[ExportFunctions] method '" + containerFullName + "." + methodName +
            "' is not an eligible function: " + RejectionMessage(rejection);

        /// <summary>The ineligible-container message, so the runtime's <c>ArgumentException</c> and the build
        /// tier's <c>HED7021</c> say the same thing about the same host wiring mistake.</summary>
        internal static string ContainerIneligibleMessage(string containerFullName) =>
            "[ExportFunctions] container '" + containerFullName + "' must be a public static class.";

        /// <summary>Signature identity for the replace-on-identical rule. Keys are compared only <b>within</b> a
        /// tier (each tier merges its own registrations), so the two sides need consistent spellings, not
        /// identical ones — the reflection side uses <c>Type.FullName</c>, the symbol side its non-aliased
        /// fully-qualified display.</summary>
        internal static bool SameSignature(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a == null || b == null || a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (!string.Equals(a[i], b[i], System.StringComparison.Ordinal))
                    return false;
            return true;
        }
    }
}
