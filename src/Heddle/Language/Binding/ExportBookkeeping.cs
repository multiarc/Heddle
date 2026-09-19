using System.Collections.Generic;

namespace Heddle.Language.Binding
{
    internal sealed class ExportOverload<TPayload>
    {
        public string ContainerAqn;

        public IReadOnlyList<string> ParameterTypeKeys;

        public TPayload Payload;
    }

    /// <summary>Produces the manifest's per-(name, container) overload counts, which the gauntlet compares
    /// against the live registry under AddOrReplace merge semantics (a second container adds its overloads; only
    /// identical signatures replace).</summary>
    internal sealed class ExportBookkeeping<TPayload>
    {
        private readonly Dictionary<string, List<ExportOverload<TPayload>>> _byName =
            new Dictionary<string, List<ExportOverload<TPayload>>>(System.StringComparer.Ordinal);

        private readonly List<string> _names = new List<string>();

        internal IReadOnlyList<string> Names => _names;

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

        internal List<string> Containers(string functionName)
        {
            var containers = new List<string>();
            foreach (var overload in Overloads(functionName))
                if (!containers.Contains(overload.ContainerAqn))
                    containers.Add(overload.ContainerAqn);
            return containers;
        }

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
