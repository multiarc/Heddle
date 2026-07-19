namespace Heddle.Precompiled
{
    /// <summary>One called function name and its actual build-time binding target (phase 7 D21, per the OQ1
    /// resolution): the discovered exporting container as AQN sans version (e.g.
    /// <c>"Acme.Web.TemplateFunctions, Acme.Web"</c>), or the shim's forwarding-target type
    /// (<c>"Heddle.Runtime.Expressions.BuiltInFunctions, Heddle"</c>) for shim-bound defaults. One row per distinct
    /// <c>(name, target)</c> pair the generated code calls; a merged overload set spanning targets carries one row
    /// per target. <see cref="OverloadCount"/> is the merged table's per-target overload count for the name at build
    /// (0 on a null-target row). A null <see cref="TargetTypeName"/> marks a name resolvable from neither the default
    /// table nor any referenced export (delegate-only remainder) and appears only on fallback-marker entries.</summary>
    public readonly struct PrecompiledFunctionBinding
    {
        public PrecompiledFunctionBinding(string name, string targetTypeName, int overloadCount)
        {
            Name = name;
            TargetTypeName = targetTypeName;
            OverloadCount = overloadCount;
        }

        public string Name { get; }

        /// <summary>The bound target as AQN sans version, or <c>null</c> for the unresolvable-at-build remainder.</summary>
        public string TargetTypeName { get; }

        public int OverloadCount { get; }
    }
}
