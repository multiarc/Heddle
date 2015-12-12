using System;
using Templates.Data;

namespace Templates.Runtime.Parameters {

    internal interface IRuntimeParameter : IDisposable
    {
        object GetParameter(Scope scope);
    }
}