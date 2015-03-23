using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Templates.Native {
    public static class NativeHelper
    {
        private static readonly AllocatorDelegate Allocator;
        private static readonly MemcpyDelegate Memcpy;
#if ASPNETCORE50
        private static readonly GetCurrentDomainDelegate GetCurrentDomain;
#endif

        public static unsafe void MemCpy(char* dest, char* src, int len)
        {
            if (len > 0 && dest != null && src != null)
#if ASPNETCORE50
                Buffer.MemoryCopy(src, dest, len * 2, len * 2);
#else
                Memcpy(dest, src, len);
#endif
        }

        public static string AllocateDefault(int length)
        {
            return new string('\0', length);
        }

        //Copied implementation of coreclr CompareOrdinal
        public static unsafe int Equals(char* one, char* two, int lenOne, int lenTwo)
        {
            if (one == two) {
                return 0;
            }
            char* a = one;
            char* b = two;
            int length = Math.Min(lenOne, lenTwo);
            int diffOffset = -1;

            // unroll the loop
            while (length >= 10) {
                if (*(int*)a != *(int*)b) {
                    diffOffset = 0;
                    break;
                }

                if (*(int*)(a+2) != *(int*)(b+2)) {
                    diffOffset = 2;
                    break;
                }

                if (*(int*)(a+4) != *(int*)(b+4)) {
                    diffOffset = 4;
                    break;
                }

                if (*(int*)(a+6) != *(int*)(b+6)) {
                    diffOffset = 6;
                    break;
                }

                if (*(int*)(a+8) != *(int*)(b+8)) {
                    diffOffset = 8;
                    break;
                }
                a += 10;
                b += 10;
                length -= 10;
            }

            if (diffOffset != -1) {
                // we already see a difference in the unrolled loop above
                a += diffOffset;
                b += diffOffset;
                int order;
                if ((order = (int)*a - (int)*b) != 0) {
                    return order;
                }
                return ((int)*(a+1) - (int)*(b+1));
            }

            // now go back to slower code path and do comparison on 4 bytes one time. 
            // Following code also take advantage of the fact strings will
            // use even numbers of characters (runtime will have a extra zero at the end.) 
            // so even if length is 1 here, we can still do the comparsion. 
            while (length > 0) {
                if (*(int*)a != *(int*)b) {
                    break;
                }
                a += 2;
                b += 2;
                length -= 2;
            }

            if (length > 0) {
                int c;
                // found a different int on above loop
                if ((c = (int)*a - (int)*b) != 0) {
                    return c;
                }
                return ((int)*(a+1) - (int)*(b+1));
            }
            return lenOne - lenTwo;
        }

        static NativeHelper()
        {
            try
            {
                MethodInfo method = typeof (string).GetMethod("FastAllocateString", BindingFlags.Static | BindingFlags.NonPublic);
                if (method != null)
                {
                    Allocator = (AllocatorDelegate) method.CreateDelegate(typeof (AllocatorDelegate));
                }
                method = typeof (string).GetMethod("wstrcpy", BindingFlags.Static | BindingFlags.NonPublic);
                if (method == null)
                {
                    throw new InvalidOperationException("Cannot find string.wstrcpy() system method");
                }
                Memcpy = (MemcpyDelegate)method.CreateDelegate(typeof(MemcpyDelegate));
#if ASPNETCORE50
                method = typeof(object).GetTypeInfo().Assembly.GetType("AppDomain").GetProperty("CurrentDomain", BindingFlags.Public | BindingFlags.Static).GetGetMethod();
                if (method == null) {
                    throw new InvalidOperationException("Cannot find AppDomain.CurrentDomain Property get method in Core Mode, investigate to issue and rewrite assembly list acquire");
                }
                GetCurrentDomain = (GetCurrentDomainDelegate)method.CreateDelegate(typeof(GetCurrentDomainDelegate));
#endif
            }
            catch (Exception)
            {
                //TODO: Log error here
            }
        }

#if ASPNETCORE50
        public static Assembly[] GetAssemblies() {
            var domain = GetCurrentDomain();
            var method = typeof(object).GetTypeInfo().Assembly.GetType("AppDomain").GetMethod("GetAssemblies", BindingFlags.Public | BindingFlags.Instance);
            return ((GetAssembliesDelegate)method.CreateDelegate(typeof(GetAssembliesDelegate), domain))();
        }
#endif

        public static string AllocateString(int length)
        {
            if (Allocator == null)
                return AllocateDefault(length);
            return Allocator(length);
        }

        public static unsafe int StartsWith(char* data, char* find, int* needleTable, int dataLen, int findLen)
        {
            if (dataLen >= findLen)
            {
                int found = 0;
                int currentIndex = findLen - 1;
                int counter = currentIndex;

                while (counter >= 0 && currentIndex < dataLen)
                {
                    counter = findLen - 1;
                    found = currentIndex;
                    while (counter >= 0 && data[found] == find[counter])
                    {
                        found--;
                        counter--;
                    }
                    currentIndex += needleTable[(sbyte) data[currentIndex]];
                }
                found++;
                if (found <= dataLen - findLen)
                    return found;
            }
            return -1;
        }

        public static bool IsWhiteSpace(char c)
        {
            return c == ' ' || c == '\x00a0' || c == '\x0085' || c >= '\x0009' && c <= '\x000d';
        }
    }
}