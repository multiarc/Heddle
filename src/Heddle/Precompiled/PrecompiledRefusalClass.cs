namespace Heddle.Precompiled
{
    /// <summary>The refusal class of one site the build could not precompile.</summary>
    public enum PrecompiledRefusalClass
    {
        /// <summary>The bound extension type declares itself not precompilable.</summary>
        UnsupportedExtension,

        /// <summary>A value the engine types by reflection enumeration order.</summary>
        ReflectionOrderValue,

        /// <summary>A bodied or chained consumer over a call the build registry cannot bind.</summary>
        UnbindableCallTyping
    }
}
