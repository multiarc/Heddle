using Templates.Data;

namespace Templates.Runtime
{
    internal interface IProcessStrategy
    {
        string Execute(in Scope scope);

        void Render(in Scope scope);
    }
}