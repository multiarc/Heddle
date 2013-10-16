// <copyright file="MethodExtensionTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Extensions;
using Microsoft.Pex.Framework.Generated;
using Templates.Exceptions;
using Templates.Core;
using Microsoft.Pex.Framework.Pointers;

namespace Templates.Extensions
{
    [TestClass]
    [PexClass(typeof(MethodExtension))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class MethodExtensionTest
    {
        [PexMethod, PexAllowedException(typeof(TemplateProcessingException)), PexAllowedException(typeof(TemplateProcessingException))]
        public object ProcessData(
            [PexAssumeUnderTest]MethodExtension target,
            object value,
            object additionalValue
        )
        {
            object result = target.ProcessData(value, additionalValue);
            return result;
            // TODO: add assertions to method MethodExtensionTest.ProcessData(MethodExtension, Object, Object)
        }
        [TestMethod]
        public void ProcessDataThrowsTemplateProcessingException99()
        {
            MethodExtension methodExtension;
            object o;
            methodExtension = new MethodExtension();
            o = this.ProcessData(methodExtension, (object)null, (object)null);
        }
        [PexMethod, PexAllowedException(typeof(TemplateCompileException)), PexAllowedException(typeof(TemplateCompileException))]
        public Type InitializeInnerTemplate(
            [PexAssumeUnderTest]MethodExtension target,
            FastString parameter,
            Type dataType,
            Type additionalType,
            CompileContext context
        )
        {
            Type result = target.InitializeInnerTemplate(parameter, dataType, additionalType, context);
            return result;
            // TODO: add assertions to method MethodExtensionTest.InitializeInnerTemplate(MethodExtension, FastString, Type, Type, CompileContext)
        }
        [TestMethod]
        public void InitializeInnerTemplateThrowsTemplateCompileException537()
        {
            MethodExtension methodExtension;
            Type type;
            methodExtension = new MethodExtension();
            type = this.InitializeInnerTemplate(methodExtension, (FastString)null,
                                                (Type)null, (Type)null, (CompileContext)null);
        }
        [TestMethod]
        public unsafe void InitializeInnerTemplateThrowsTemplateCompileException52()
        {
            /* 
            MethodExtension methodExtension;
            FastString fastString;
            Type type;
            uint @base = 174063616u;
            PexPointerSpace.Initialize(new UIntPtr(@base), new UIntPtr(65536u));
            methodExtension = new MethodExtension();
            fastString = new FastString(unchecked((char*)0), 0);
            type = this.InitializeInnerTemplate
                       (methodExtension, fastString, (Type)null, (Type)null, (CompileContext)null);
            PexPointerSpace.Validate();
            */
        }
        [TestMethod]
        public unsafe void InitializeInnerTemplateThrowsTemplateCompileException351()
        {
            /* 
            MethodExtension methodExtension;
            FastString fastString;
            Type type;
            uint @base = 174063616u;
            PexPointerSpace.Initialize(new UIntPtr(@base), new UIntPtr(65536u));
            methodExtension = new MethodExtension();
            fastString = new FastString(unchecked((char*)174096441), 1);
            type = this.InitializeInnerTemplate
                       (methodExtension, fastString, (Type)null, (Type)null, (CompileContext)null);
            PexPointerSpace.Validate();
            */
        }
        
        [TestMethod]
        public void InitializeInnerTemplateThrowsTemplateCompileException882()
        {
            MethodExtension methodExtension;
            Type type;
            methodExtension = new MethodExtension();
            type = this.InitializeInnerTemplate(methodExtension, (FastString)null,
                                                (Type)null, (Type)null, (CompileContext)null);
        }
        [TestMethod]
        public unsafe void InitializeInnerTemplateThrowsTemplateCompileException166()
        {
            /* 
            MethodExtension methodExtension;
            FastString fastString;
            Type type;
            uint @base = 499122176u;
            PexPointerSpace.Initialize(new UIntPtr(@base), new UIntPtr(65536u));
            methodExtension = new MethodExtension();
            fastString = new FastString(unchecked((char*)0), 0);
            type = this.InitializeInnerTemplate
                       (methodExtension, fastString, (Type)null, (Type)null, (CompileContext)null);
            PexPointerSpace.Validate();
            */
        }
        [TestMethod]
        public unsafe void InitializeInnerTemplateThrowsTemplateCompileException942()
        {
            /* 
            MethodExtension methodExtension;
            FastString fastString;
            Type type;
            uint @base = 499122176u;
            PexPointerSpace.Initialize(new UIntPtr(@base), new UIntPtr(65536u));
            methodExtension = new MethodExtension();
            fastString = new FastString(unchecked((char*)499155005), 1);
            type = this.InitializeInnerTemplate
                       (methodExtension, fastString, (Type)null, (Type)null, (CompileContext)null);
            PexPointerSpace.Validate();
            */
        }
        
        [TestMethod]
        public void ProcessDataThrowsTemplateProcessingException170()
        {
            MethodExtension methodExtension;
            object o;
            methodExtension = new MethodExtension();
            o = this.ProcessData(methodExtension, (object)null, (object)null);
        }
    }
}
