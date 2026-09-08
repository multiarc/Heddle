namespace Heddle.Precompiled
{
    /// <summary>The built-in target identity the gauntlet compares function rows against: a row whose recorded
    /// target is this name is a default built-in call. The overload table itself is gone with the 2.x generator —
    /// the build tier binds what it resolves and the live <c>FunctionRegistry.Default</c> is the other side of
    /// every comparison.</summary>
    internal static class DefaultFunctionTable
    {
        /// <summary>Assembly-qualified type name (without version) that the default built-ins resolve to.</summary>
        public const string ShimTargetTypeName = "Heddle.Runtime.Expressions.BuiltInFunctions, Heddle";
    }
}
