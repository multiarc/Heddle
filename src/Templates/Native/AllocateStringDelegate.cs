namespace Templates.Native
{
    internal delegate string Allocate(int len);

    internal delegate string ConcatArray(string[] values, int totalLength);
}