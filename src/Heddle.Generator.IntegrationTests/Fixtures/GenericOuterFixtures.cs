namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>A generic whose nested type declares no type parameter of its own. Naming the nested type is the
    /// only place the outer's argument can be seen, and the two tiers count that argument differently: reflection
    /// reports it among the nested type's generic arguments, Roslyn does not report it in the nested symbol's
    /// arity. A nested type under a non-generic outer cannot tell the two counts apart.</summary>
    public sealed class ArityOuter<T>
    {
        public sealed class ArityInner
        {
            public int Amount { get; set; }
        }
    }
}
