using System;
using System.Runtime.CompilerServices;
using Heddle.Data;

namespace Heddle.Runtime.Parameters {

    internal interface IRuntimeParameter : IDisposable
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        object GetParameter(in Scope scope);
    }
}