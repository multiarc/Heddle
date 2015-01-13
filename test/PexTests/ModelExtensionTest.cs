// <copyright file="ModelExtensionTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Extensions;
using Templates.Core;
using Microsoft.Pex.Framework.Generated;
using Templates.Exceptions;
using Microsoft.Pex.Framework.Pointers;

namespace Templates.Extensions
{
    [TestClass]
    [PexClass(typeof(ModelExtension))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class ModelExtensionTest
    {
        [PexMethod, PexAllowedException(typeof(TemplateInitException))]
        public Type InitializeInnerTemplate(
            [PexAssumeUnderTest]ModelExtension target,
            FastString parameter,
            Type dataType,
            Type additionalType,
            CompileContext context
        )
        {
            Type result = target.InitializeInnerTemplate(parameter, dataType, additionalType, context);
            return result;
            // TODO: add assertions to method ModelExtensionTest.InitializeInnerTemplate(ModelExtension, FastString, Type, Type, CompileContext)
        }
        [TestMethod]
        public void InitializeInnerTemplateThrowsTemplateInitException834()
        {
            ModelExtension modelExtension;
            Type type;
            modelExtension = new ModelExtension();
            type = this.InitializeInnerTemplate(modelExtension, (FastString)null,
                                                (Type)null, (Type)null, (CompileContext)null);
        }
        [TestMethod]
        public unsafe void InitializeInnerTemplateThrowsTemplateInitException836()
        {
            /* 
            ModelExtension modelExtension;
            FastString fastString;
            Type type;
            uint @base = 501219328u;
            PexPointerSpace.Initialize(new UIntPtr(@base), new UIntPtr(65536u));
            modelExtension = new ModelExtension();
            fastString = new FastString(unchecked((char*)501252157), 1);
            type = this.InitializeInnerTemplate
                       (modelExtension, fastString, (Type)null, (Type)null, (CompileContext)null);
            PexPointerSpace.Validate();
            */
        }
    }
}
