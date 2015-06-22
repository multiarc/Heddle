using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Templates.Native
{
    public delegate void VoidFunc<in T>(T arg1);
    public delegate void VoidFunc<in T1, in T2>(T1 arg1, T2 arg2);
    public delegate void VoidFunc<in T1, in T2, in T3>(T1 arg1, T2 arg2, T3 arg3);
    public delegate void VoidFunc<in T1, in T2, in T3, in T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate void VoidFunc<in T1, in T2, in T3, in T4, in T5>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
}
