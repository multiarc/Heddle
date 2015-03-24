using System;

namespace Templates.Native
{
    internal unsafe delegate void MemcpyDelegate(char* dest, char* src, int len);
}