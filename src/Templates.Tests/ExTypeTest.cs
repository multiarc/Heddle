using Templates.Exceptions;
// <copyright file="ExTypeTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Data;

namespace Templates.Data
{
    [TestClass]
    [PexClass(typeof(ExType))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public class ExTypeTest
    {
        [PexMethod]
        [PexAllowedException(typeof(ArgumentException))]
        [PexAllowedException(typeof(TemplateCompileException))]
        public ExType Constructor02(string name, string[] imports) {
            ExType target = new ExType(name, imports);
            return target;
            // TODO: add assertions to method ExTypeTest.Constructor02(String, String[])
        }
    }
}
