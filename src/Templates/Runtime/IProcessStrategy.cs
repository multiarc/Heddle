using Templates.Data;

namespace Templates.Runtime
{
    internal interface IProcessStrategy
    {
        string Execute(ref Scope scope);
    }
}