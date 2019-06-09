using System;
using System.Runtime.CompilerServices;
using Templates.Data;

namespace Templates.Runtime.Parameters {

    internal interface IRuntimeParameter : IDisposable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        object GetParameter(in Scope scope);
    }
}