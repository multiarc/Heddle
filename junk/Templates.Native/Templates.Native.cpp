#include "stdafx.h"

#include "Templates.Native.h"

namespace Templates 
{
	namespace Native
	{
		void NativeHelper::MemCpy(wchar_t* dest, wchar_t* src, int len)
		{
			if (len > 0 && dest != NULL && src != NULL)
				memcpy(dest, src, len * 2);
		}

		int NativeHelper::Equals(wchar_t* compareTo, wchar_t* compareWith, int lenTo, int lenWith)
		{
			if (lenTo < lenWith)
				return -1;
			if (lenTo > lenWith)
				return 1;
			return wmemcmp(compareTo, compareWith, lenTo);
		}

		//String^ NativeHelper::AllocateDefault(int length)
		//{
		//	return gcnew String('\0', length);
		//}

		//static NativeHelper::NativeHelper()
		//{
		//	try
		//	{
		//		MethodInfo^ method = String::typeid->GetMethod("FastAllocateString", BindingFlags::Static | BindingFlags::NonPublic);
		//		if (method != nullptr)
		//		{
		//			Allocator = reinterpret_cast<AllocatorType>(method->MethodHandle.GetFunctionPointer().ToPointer());
		//		}
		//	}
		//	catch (Exception^)
		//	{
		//		//TODO: Log error here
		//	}
		//}

		//String^ NativeHelper::AllocateString(int length)
		//{
		//	if (Allocator == NULL)
		//		return AllocateDefault(length);
		//	return Allocator(length);
		//}

		int NativeHelper::StartsWith(wchar_t* data, wchar_t* find, int* needleTable, int dataLen, int findLen)
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
					currentIndex += needleTable[(byte)data[currentIndex]];
				}
				found++;
				if (found <= dataLen - findLen)
					return found;
			}
			return -1;
		}

		bool NativeHelper::IsWhiteSpace(wchar_t c)
		{
			return c == ' ' || c == '\x00a0' || c == '\x0085' || c >= '\x0009' && c <= '\x000d';
		}
	}
}