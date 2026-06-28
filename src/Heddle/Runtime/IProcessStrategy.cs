using Heddle.Data;

namespace Heddle.Runtime
{
    internal interface IProcessStrategy
    {
        string Execute(in Scope scope);

        void Render(in Scope scope);
    }
}