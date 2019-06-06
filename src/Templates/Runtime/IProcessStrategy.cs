using Templates.Data;

namespace Templates.Runtime
{
    internal interface IProcessStrategy
    {
        void Execute(ref Scope scope);
    }
}