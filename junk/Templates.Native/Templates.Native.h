// Templates.Native.h

#pragma once

using namespace System;
using namespace System::Reflection;

#define uint unsigned int
#define byte unsigned char

namespace Templates
{
	namespace Native
	{
		//typedef String ^ (*AllocatorType)(int);

		public ref class NativeHelper
		{
		private:
			//static AllocatorType Allocator;
			//static NativeHelper();
			//static String^ AllocateDefault(int);
		public:
			static void MemCpy(wchar_t*, wchar_t*, int);
			static int Equals(wchar_t*, wchar_t*, int, int);
			static int StartsWith(wchar_t*, wchar_t*, int*, int, int);
			static bool IsWhiteSpace(wchar_t);
			//static String^ AllocateString(int);
		};
	}
}