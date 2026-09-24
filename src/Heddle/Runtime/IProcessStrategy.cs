using Heddle.Data;

namespace Heddle.Runtime
{
    /// <summary>
    /// A compiled template body. Public because generated code in the consuming assembly implements it, and the
    /// precompiled runtime registry surfaces it as
    /// <see cref="Heddle.Precompiled.PrecompiledTemplateInfo.Strategy"/>.
    /// <see cref="Execute"/> returns the body's string (the <c>NormalStrategy</c> concat path) and
    /// <see cref="Render"/> writes it to the scope's renderer (the hot path).
    /// </summary>
    public interface IProcessStrategy
    {
        string Execute(in Scope scope);

        void Render(in Scope scope);
    }
}