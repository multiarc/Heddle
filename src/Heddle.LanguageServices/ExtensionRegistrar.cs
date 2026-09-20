using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Attributes;
using Heddle.Runtime;

namespace Heddle.LanguageServices
{
    /// <summary>
    /// The workspace extension-export scan: reads assembly-level <c>[ExportExtensions]</c> from the workspace's
    /// loaded model assemblies and offers those extensions to the real <see cref="TemplateFactory"/> — the same
    /// two branches the engine's own registration runs. The engine's registry is process-wide, so what a
    /// workspace exports is kept as that workspace's layer over the registry as the server first found it:
    /// configuring another workspace, reloading a rebuilt one or closing one republishes the layers, and nothing
    /// a previous workspace exported lingers.
    /// </summary>
    internal static class ExtensionRegistrar
    {
        private static readonly object Gate = new object();
        private static IReadOnlyDictionary<string, Type> _baseline;
        private static readonly List<KeyValuePair<object, IReadOnlyList<ExtensionType>>> Layers =
            new List<KeyValuePair<object, IReadOnlyList<ExtensionType>>>();

        /// <summary>Makes <paramref name="assemblies"/>' exported extensions the layer of <paramref name="owner"/>,
        /// replacing whatever it published before; an empty set withdraws it.</summary>
        internal static void Publish(object owner, IReadOnlyList<Assembly> assemblies, Action<string> log)
        {
            var exported = new List<ExtensionType>();
            if (assemblies != null)
            {
                foreach (var assembly in assemblies)
                    exported.AddRange(ExportedBy(assembly, log));
            }

            lock (Gate)
            {
                int index = Layers.FindIndex(layer => ReferenceEquals(layer.Key, owner));
                if (index < 0 && exported.Count == 0)
                    return;
                if (_baseline == null)
                    _baseline = TemplateFactory.CaptureRegistry();
                if (index >= 0)
                    Layers.RemoveAt(index);
                if (exported.Count > 0)
                    Layers.Add(new KeyValuePair<object, IReadOnlyList<ExtensionType>>(owner, exported));

                var layers = new List<IReadOnlyList<ExtensionType>>();
                foreach (var layer in Layers)
                    layers.Add(layer.Value);
                TemplateFactory.PublishLayers(_baseline, layers, (rejected, e) =>
                {
                    if (ReferenceEquals(rejected, exported))
                        log?.Invoke($"Extension override rejected: {e.Message}");
                });
            }
        }

        internal static void Withdraw(object owner)
        {
            Publish(owner, null, null);
        }

        private static List<ExtensionType> ExportedBy(Assembly assembly, Action<string> log)
        {
            var found = new List<ExtensionType>();
            try
            {
                foreach (var attribute in assembly.GetCustomAttributes<ExportExtensionsAttribute>())
                {
                    if (attribute == null)
                        continue;
                    found.AddRange(attribute.All
                        ? TemplateFactory.LoadExtensions(assembly)
                        : TemplateFactory.LoadExtensions(attribute.Extensions));
                    if (attribute.All)
                        break;
                }
            }
            catch (Exception e) when (WorkspaceReflection.IsLoadFault(e))
            {
                found.Clear();
                log?.Invoke("Extension exports of '" + assembly.GetName().Name + "' skipped: " +
                    WorkspaceReflection.Describe(e));
            }

            return found;
        }
    }
}
