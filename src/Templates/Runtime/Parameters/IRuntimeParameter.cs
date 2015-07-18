using System;

namespace Templates.Runtime.Parameters {

    internal interface IRuntimeParameter : IDisposable
    {
        object GetParameter(object value, object chainedResult);
    }
}