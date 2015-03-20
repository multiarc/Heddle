using Templates.Exceptions;
// <copyright file="ReflectionHelperTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Helpers;

namespace Templates.Helpers
{
    [TestClass]
    [PexClass(typeof(ReflectionHelper))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class ReflectionHelperTest
    {
        [PexMethod]
        [PexAllowedException(typeof(TemplateCompileException))]
        [PexAllowedException(typeof(ArgumentException))]
        public Type ResolveType(string typeName, string[] imports) {
            Type result = ReflectionHelper.ResolveType(typeName, imports);
            return result;
            // TODO: add assertions to method ReflectionHelperTest.ResolveType(String, String[])
        }
        [PexMethod]
        [PexAllowedException(typeof(MissingMethodException))]
        public object Invoke(
            [PexAssumeUnderTest]ReflectionHelper target,
            object o,
            string methodName,
            object[] parameters
        ) {
            object result = target.Invoke(o, methodName, parameters);
            return result;
            // TODO: add assertions to method ReflectionHelperTest.Invoke(ReflectionHelper, Object, String, Object[])
        }
        [PexGenericArguments(typeof(int))]
        [PexMethod]
        [PexAllowedException(typeof(MissingMethodException))]
        public T Invoke01<T>(
            [PexAssumeUnderTest]ReflectionHelper target,
            object o,
            string methodName,
            object[] parameters
        ) {
            T result = target.Invoke<T>(o, methodName, parameters);
            return result;
            // TODO: add assertions to method ReflectionHelperTest.Invoke01(ReflectionHelper, Object, String, Object[])
        }
    }
}
