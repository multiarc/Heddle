using System.Collections.Generic;

namespace Heddle.Language.Binding
{
    /// <summary>One registered export overload, as the bookkeeping sees it.</summary>
    internal sealed class ExportOverload<TPayload>
    {
        /// <summary>The declaring container's manifest identity (<c>AqnFormatter</c> output).</summary>
        public string ContainerAqn;

        /// <summary>Ordered parameter-type keys — the signature identity.</summary>
        public IReadOnlyList<string> ParameterTypeKeys;

        /// <summary>The adapter's own per-overload state (a <c>MethodInfo</c>, a method symbol, a cased name).</summary>
        public TPayload Payload;
    }

    /// <summary>
    /// Phase 3 (F2/OQ2): the runtime's <c>AddOrReplace</c> merge semantics, stated once — a second container
    /// exporting the same function name <b>adds</b> its overloads to the same name, and only an identical signature
    /// replaces. The generator's first-container-wins rule is what this replaces: two containers exporting
    /// <c>slugify</c> merged at run time while the build tier bound only the first, so the gauntlet saw a live
    /// target absent from the manifest's recorded set and fell back on every render, permanently.
    /// <para>The bookkeeping is what produces the manifest's per-(name, container) overload <b>counts</b>, which
    /// the gauntlet compares exactly in both directions.</para>
    /// </summary>
    internal sealed class ExportBookkeeping<TPayload>
    {
        private readonly Dictionary<string, List<ExportOverload<TPayload>>> _byName =
            new Dictionary<string, List<ExportOverload<TPayload>>>(System.StringComparer.Ordinal);

        private readonly List<string> _names = new List<string>();

        /// <summary>Function names in first-registration order.</summary>
        internal IReadOnlyList<string> Names => _names;

        /// <summary>Adds (or replaces on identical signature) one overload under <paramref name="functionName"/>.
        /// Returns true when a new overload was appended, false when an existing one was replaced.</summary>
        internal bool AddOrReplace(string functionName, ExportOverload<TPayload> overload)
        {
            if (!_byName.TryGetValue(functionName, out var overloads))
            {
                _byName[functionName] = overloads = new List<ExportOverload<TPayload>>();
                _names.Add(functionName);
            }

            for (int i = 0; i < overloads.Count; i++)
            {
                if (ExportRules.SameSignature(overloads[i].ParameterTypeKeys, overload.ParameterTypeKeys))
                {
                    overloads[i] = overload;
                    return false;
                }
            }

            overloads.Add(overload);
            return true;
        }

        internal IReadOnlyList<ExportOverload<TPayload>> Overloads(string functionName) =>
            _byName.TryGetValue(functionName, out var overloads)
                ? (IReadOnlyList<ExportOverload<TPayload>>) overloads
                : new ExportOverload<TPayload>[0];

        /// <summary>The distinct containers contributing to a function name, in registration order — one manifest
        /// row per container, each with its own overload count.</summary>
        internal List<string> Containers(string functionName)
        {
            var containers = new List<string>();
            foreach (var overload in Overloads(functionName))
                if (!containers.Contains(overload.ContainerAqn))
                    containers.Add(overload.ContainerAqn);
            return containers;
        }

        /// <summary>How many overloads of <paramref name="functionName"/> the given container contributes — the
        /// manifest row's count, and the number the gauntlet compares against the live registry.</summary>
        internal int OverloadCount(string functionName, string containerAqn)
        {
            int count = 0;
            foreach (var overload in Overloads(functionName))
                if (string.Equals(overload.ContainerAqn, containerAqn, System.StringComparison.Ordinal))
                    count++;
            return count;
        }
    }
}
