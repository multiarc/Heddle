using System.Collections.Generic;
// <copyright file="SmartListTTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Collections;

namespace Templates.Collections
{
    [TestClass]
    [PexClass(typeof(SmartList<>))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class SmartListTTest
    {
        [PexGenericArguments(typeof(int))]
        [PexMethod(MaxConditions = 2000, MaxRunsWithoutNewTests = 200)]
        public SmartList<T> AddRange01<T>([PexAssumeUnderTest]SmartList<T> target, IEnumerable<T> items) {
            SmartList<T> result = target.AddRange(items);
            return result;
            // TODO: add assertions to method SmartListTTest.AddRange01(SmartList`1<!!0>, IEnumerable`1<!!0>)
        }
        [PexGenericArguments(typeof(int))]
        [PexMethod(MaxConditions = 2000, MaxRunsWithoutNewTests = 200)]
        public SmartList<T> AddRange<T>([PexAssumeUnderTest]SmartList<T> target, ICollection<T> items) {
            SmartList<T> result = target.AddRange(items);
            return result;
            // TODO: add assertions to method SmartListTTest.AddRange(SmartList`1<!!0>, ICollection`1<!!0>)
        }
        [PexGenericArguments(typeof(int))]
        [PexMethod(MaxConditions = 2000, MaxRunsWithoutNewTests = 200)]
        public void Insert01<T>(
            [PexAssumeUnderTest]SmartList<T> target,
            int index,
            T item
        ) {
            target.Insert(index, item);
            // TODO: add assertions to method SmartListTTest.Insert01(SmartList`1<!!0>, Int32, !!0)
        }
        [PexGenericArguments(typeof(int))]
        [PexMethod(MaxConditions = 2000, MaxRunsWithoutNewTests = 200)]
        public void Insert<T>(
            [PexAssumeUnderTest]SmartList<T> target,
            int index,
            object value
        ) {
            target.Insert(index, value);
            // TODO: add assertions to method SmartListTTest.Insert(SmartList`1<!!0>, Int32, Object)
        }
        [PexGenericArguments(typeof(int))]
        [PexMethod(MaxConditions = 2000, MaxRunsWithoutNewTests = 200)]
        public void RemoveAt<T>([PexAssumeUnderTest]SmartList<T> target, int index) {
            target.RemoveAt(index);
            // TODO: add assertions to method SmartListTTest.RemoveAt(SmartList`1<!!0>, Int32)
        }
    }
}
