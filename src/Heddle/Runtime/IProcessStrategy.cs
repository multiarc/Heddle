using Heddle.Data;

namespace Heddle.Runtime
{
    /// <summary>
    /// A compiled template body. Promoted from internal to public in phase 7 (D5): generated code in the
    /// consuming assembly implements it, and the precompiled runtime registry surfaces it as
    /// <see cref="Heddle.Precompiled.PrecompiledTemplateInfo.Strategy"/>. Members unchanged from the internal
    /// contract — <see cref="Execute"/> returns the body's string (the <c>NormalStrategy</c> concat path) and
    /// <see cref="Render"/> writes it to the scope's renderer (the hot path).
    /// </summary>
    public interface IProcessStrategy
    {
        string Execute(in Scope scope);

        void Render(in Scope scope);
    }
}