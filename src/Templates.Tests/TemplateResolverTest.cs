using Templates.Exceptions;
using Templates;
using System.Collections.Generic;
using Templates.Data;
// <copyright file="TemplateResolverTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Runtime;

namespace Templates.Runtime
{
    [TestClass]
    [PexClass(typeof(TemplateResolver))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class TemplateResolverTest
    {
        [PexMethod]
        [PexAllowedException(typeof(ArgumentException))]
        [PexAllowedException(typeof(TemplateCreateException))]
        public string Search(
            [PexAssumeUnderTest]TemplateResolver target,
            string viewName,
            string controllerName,
            TemplatePathType searchType,
            out IEnumerable<string> searchedLocations,
            out TtlTemplate cached
        ) {
            string result
               = target.Search(viewName, controllerName, searchType, out searchedLocations, out cached);
            return result;
            // TODO: add assertions to method TemplateResolverTest.Search(TemplateResolver, String, String, TemplatePathType, IEnumerable`1<String>&, TtlTemplate&)
        }
        [PexMethod]
        [PexAllowedException(typeof(ArgumentException))]
        public TemplateResolver Constructor(string rootPath, bool checkFileChange) {
            TemplateResolver target = new TemplateResolver(rootPath, checkFileChange);
            return target;
            // TODO: add assertions to method TemplateResolverTest.Constructor(String, Boolean)
        }
        [PexMethod]
        [PexAllowedException(typeof(ArgumentException))]
        [PexAllowedException(typeof(TemplateCreateException))]
        public TtlTemplate GetTemplate(
            [PexAssumeUnderTest]TemplateResolver target,
            string viewName,
            string controllerName,
            out IEnumerable<string> searchedLocations,
            CompileContext context,
            TemplatePathType searchType
        ) {
            TtlTemplate result
               = target.GetTemplate(viewName, controllerName, out searchedLocations, context, searchType);
            return result;
            // TODO: add assertions to method TemplateResolverTest.GetTemplate(TemplateResolver, String, String, IEnumerable`1<String>&, CompileContext, TemplatePathType)
        }
    }
}
