namespace Heddle.Precompiled
{
    /// <summary>A function name and its build-time binding target (AQN sans version, or null if unresolvable).</summary>
    public readonly struct PrecompiledFunctionBinding
    {
        public PrecompiledFunctionBinding(string name, string targetTypeName, int overloadCount)
        {
            Name = name;
            TargetTypeName = targetTypeName;
            OverloadCount = overloadCount;
        }

        public string Name { get; }

        /// <summary>Bound target as AQN sans version, or <c>null</c> if unresolvable at build.</summary>
        public string TargetTypeName { get; }

        public int OverloadCount { get; }
    }
}
