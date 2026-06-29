namespace Heddle.Native
{
#if NET10_0_OR_GREATER
    internal delegate string Allocate(nint len);
#else
    internal delegate string Allocate(int len);
#endif
}
