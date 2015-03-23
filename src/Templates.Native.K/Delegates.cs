using System.Reflection;

namespace Templates.Native
{
    internal unsafe delegate void MemcpyDelegate(char* dmem, char* smem, int charCount);
    internal delegate string AllocatorDelegate(int len);
#if ASPNETCORE50
    internal delegate object GetCurrentDomainDelegate();
    internal delegate Assembly[] GetAssembliesDelegate();
#endif
}