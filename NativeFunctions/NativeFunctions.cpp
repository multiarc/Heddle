// This is the main DLL file.

#include "stdafx.h"

#include "NativeFunctions.h"

namespace NativeFunctions
{
	void StringNativeHelper::MemCpy(wchar_t* dest, wchar_t* src, int len)
	{
		if (len > 0 && dest != NULL && src != NULL)
			memcpy(dest,src,len << 1);
	}

	size_t StringNativeHelper::StrLen(wchar_t* value)
	{
		return wcslen(value);
	}

	int StringNativeHelper::Equals (wchar_t* compareTo, wchar_t* compareWith, int lenTo, int lenWith)
    {
        if (lenTo < lenWith)
            return -1;
		if (lenTo > lenWith)
			return 1;
		/*while (lenTo >= 16) {
            if (((ulong*) compareTo)[0] != ((ulong*) compareWith)[0] || ((ulong*) compareTo)[1] != ((ulong*) compareWith)[1]
                || ((ulong*) compareTo)[2] != ((ulong*) compareWith)[2] || ((ulong*) compareTo)[3] != ((ulong*) compareWith)[3])
                return false;
            lenTo -= 16;
            compareTo += 16;
            compareWith += 16;
        }
        if ((lenTo & 8) != 0) {
            if (((ulong*) compareTo)[0] != ((ulong*) compareWith)[0] || ((ulong*) compareTo)[1] != ((ulong*) compareWith)[1])
                return false;
            lenTo -= 8;
            compareTo += 8;
            compareWith += 8;
        }
        if ((lenTo & 4) != 0) {
            if (((ulong*) compareTo)[0] != ((ulong*) compareWith)[0])
                return false;
            lenTo -= 4;
            compareTo += 4;
            compareWith += 4;
        }
        if ((lenTo & 2) != 0) {
            if (((uint*) compareTo)[0] != ((uint*) compareWith)[0])
                return false;
            lenTo -= 2;
            compareTo += 2;
            compareWith += 2;
        }
        if ((lenTo & 1) != 0) {
            if (compareTo[0] != compareWith[0])
                return false;
        }*/
        return wmemcmp(compareTo, compareWith, lenTo);
    }

	String^ StringNativeHelper::AllocateDefault(int length)
	{
		return gcnew String('\0', length);
	}

	static StringNativeHelper::StringNativeHelper()
	{
		try
		{
			MethodInfo^ method = String::typeid->GetMethod("FastAllocateString", BindingFlags::Static | BindingFlags::NonPublic);
			if (method != nullptr)
			{
				Allocator = reinterpret_cast<AllocatorType>(method->MethodHandle.GetFunctionPointer().ToPointer());
			}
		}
		catch(Exception^)
		{
			//TODO: Log error here
		}
	}

	String^ StringNativeHelper::AllocateString(int length)
	{
		if (Allocator == NULL)
			return AllocateDefault(length);
		return Allocator(length);
	}

	int StringNativeHelper::StartsWith (wchar_t* data, wchar_t* find, int* needleTable, int dataLen, int findLen)
    {
        if (dataLen >= findLen) {
            int found = 0;
            int currentIndex = findLen - 1;
            int counter = currentIndex;

            while (counter >= 0 && currentIndex < dataLen) {
                counter = findLen - 1;
                found = currentIndex;
                while (counter >= 0 && data[found] == find[counter]) {
                    found--;
                    counter--;
                }
                currentIndex += needleTable[(byte) data[currentIndex]];
            }
            found++;
            if (found <= dataLen - findLen)
                return found;
        }
        return -1;
    }

	bool StringNativeHelper::IsWhiteSpace(wchar_t c)
	{ 
		return c == ' ' || c == '\x00a0' || c == '\x0085' || c >= '\x0009' && c <= '\x000d';
    } 
}