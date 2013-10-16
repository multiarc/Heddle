// NativeFunctions.h

#pragma once

using namespace System;
using namespace System::Reflection;

#include <memory.h>
#include <wchar.h>
#define uint unsigned int
#define byte unsigned char

namespace NativeFunctions {

	typedef String^ (*AllocatorType)(int);

	public ref class StringNativeHelper
	{
	private:
		static AllocatorType Allocator;
		static StringNativeHelper();
		static String^ AllocateDefault(int);
	public: 
		static void MemCpy(wchar_t*, wchar_t*, int);
		static int Equals (wchar_t*, wchar_t*, int, int);
		static int StartsWith (wchar_t*, wchar_t*, int*, int, int);
		static bool IsWhiteSpace(wchar_t);
		static size_t StrLen(wchar_t*);
		static String^ AllocateString(int);
	};

	//struct Container
	//{
	//public:
	//	void* Key;
	//	void* Value;
	//};

	//struct HashItem
	//{
	//public:
	//	Container Item;
	//	int HashCode;
	//};

	//generic <class TKey,class TValue>
	//public ref class ObjectNativeCache : IDisposable
	//{
	//private:
	//	HashItem* _storage;
	//	int _length;
	//	int _capacity;

	//	int Find(TKey);
	//	int FindNearest(TKey);
	//	void BinarySearch(int, int&, int&);
	//	void Expand(int);
	//public:
	//	ObjectNativeCache() : _length(0), _capacity(0), _storage(new HashItem[0]) {};
	//	~ObjectNativeCache();
	//	TValue GetCached(TKey);
	//	void AddToCache(TKey, TValue);
	//	void Dispose();
	//	void Finalize();
	//protected:
	//	void Dispose(bool);
	//};

	//generic <class TKey,class TValue>
	//void ObjectNativeCache<TKey, TValue>::Finalize()
	//{
	//	Dispose(false);
	//}

	//generic <class TKey,class TValue>
	//void ObjectNativeCache<TKey, TValue>::Dispose()
	//{
	//	Dispose(true);
	//	GC::SupressFinalize(this);
	//}

	//generic <class TKey,class TValue>
	//void ObjectNativeCache<TKey, TValue>::Dispose(bool disposing)
	//{
	//	if (disposing) {
	//		for (int i = 0;i < _length;i++) {
	//			_storage[i].Item.Value = nullptr;
	//			_storage[i].Item.Key = nullptr;
	//		}
	//		_length = 0;
	//		_capacity = 0;
	//		delete [] _storage;
	//	}
	//}

	//generic <class TKey,class TValue>
	//ObjectNativeCache<TKey, TValue>::~ObjectNativeCache()
	//{
	//	for (int i = 0;i < _length;i++) {
	//		_storage[i].Item.Value = nullptr;
	//		_storage[i].Item.Key = nullptr;
	//	}
	//	_length = 0;
	//	_capacity = 0;
	//	delete [] _storage;
	//}

	//generic <class TKey, class TValue>
	//TValue ObjectNativeCache<TKey, TValue>::GetCached(TKey key)
	//{
	//	int index = Find(key);
	//	if (index != -1)
	//		return Marshal._storage[index].Item.Value;

	//	return nullptr;
	//}

	//generic <class TKey, class TValue>
	//void ObjectNativeCache<TKey, TValue>::BinarySearch(int hash, int& l, int& r)
	//{
	//	register int left = 0, right = _length - 1;
	//	while (right >= left)
	//	{
	//		register int middle = (left + right) / 2;
	//		if (_storage[middle].HashCode == hash)
	//		{
	//			left = middle;
	//			right = middle;
	//			while(left > 0 && _storage[left].HashCode == hash)
	//				left--;
	//			while (right < _length - 1 && _storage[right].HashCode == hash)
	//				right++;

	//			l = left;
	//			r = right;
	//		}
	//		if (left == right)
	//			break;
	//		if (_storage[middle].HashCode < hash)
	//			left = middle + 1;
	//		else
	//			right = middle;
	//	}
	//	l = -1;
	//	r = -1;
	//}

	//generic <class TKey, class TValue>
	//int ObjectNativeCache<TKey, TValue>::Find(TKey key)
	//{
	//	register int code = safe_cast<Object^>(key)->GetHashCode(); //Only managed types supported
	//	int left, right;
	//	BinarySearch(code, left, right);
	//	if (left >= 0)
	//	{
	//		while(left >= right && !safe_cast<Object^>(_storage[middle].Item.Key)->Equals(key))
	//			left++;
	//		
	//		if (left > right)
	//			return -1;

	//		return left;
	//	}
	//	return -1;
	//}

	//generic <class TKey, class TValue>
	//void ObjectNativeCache<TKey, TValue>::Expand(int len)
	//{
	//	if (_capacity < len)
	//	{
	//		if (_capacity == 0)
	//			_capacity = 2;
	//		else
	//			_capacity <<= 1;

	//		HashItem* old = _storage;
	//		_storage = new HashItem[_capacity];
	//		memcpy(_storage, old, _length * sizeof(HashItem));
	//	}
	//}
	//
	//generic <class TKey, class TValue>
	//void ObjectNativeCache<TKey, TValue>::AddToCache(TKey key, TValue value)
	//{
	//	register int index = FindNearest(key), int hash = safe_cast<Object^>(key)->GetHashCode();
	//	Expand(_length + 1);
	//	for (int i = _length;i > index;i--)
	//		_storage[i] = _storage[i-1];

	//	_storage[index].Hash = hash;
	//	_storage[index].Item.Value = value;
	//	_storage[index].Item.Key = key;
	//	_length++;
	//}

	//generic <class TKey, class TValue>
	//int ObjectNativeCache<TKey, TValue>::FindNearest(TKey key)
	//{
	//	register int right = _length - 1, left = 0, hash = safe_cast<Object^>(key)->GetHashCode();
	//	if (_length == 0)
	//		return 0;
	//	while (right >= left)
	//	{
	//		register int middle = (left + right) / 2;
	//		if (_storage[middle].Hash == hash)
	//			return middle;
	//		if (left == right)
	//		{
	//			//take last step for accurate comparing
	//			if (_storage[middle].Hash < hash)
	//				middle++;
	//			break;
	//		}
	//		if (_storage[middle].Hash < hash)
	//			left = middle + 1;
	//		else
	//			right = middle;
	//	}
	//	return middle;
	//}
}