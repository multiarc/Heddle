// <copyright file="ReflectionHelperTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Core;
using System.Collections.Generic;
using Microsoft.Pex.Framework.Generated;
using Templates.Exceptions;

namespace Templates.Core
{
    [TestClass]
    [PexClass(typeof(ReflectionHelper))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class ReflectionHelperTest
    {
        [PexMethod, PexAllowedException(typeof(ArgumentException)), PexAllowedException(typeof(TemplateCompileException))]
        public Type ResolveType(string typeName, IEnumerable<string> imports)
        {
            Type result = ReflectionHelper.ResolveType(typeName, imports);
            return result;
            // TODO: add assertions to method ReflectionHelperTest.ResolveType(String, IEnumerable`1<String>)
        }
        [TestMethod]
        public void ResolveTypeThrowsArgumentException256()
        {
            Type type;
            string[] ss = new string[0];
            type = this.ResolveType("\0", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException298()
        {
            Type type;
            string[] ss = new string[0];
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException125()
        {
            Type type;
            string[] ss = new string[1];
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException222()
        {
            Type type;
            string[] ss = new string[1];
            ss[0] = "\u0100";
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException100()
        {
            Type type;
            string[] ss = new string[2];
            ss[0] = "\u0100";
            ss[1] = "\u0100";
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException530()
        {
            Type type;
            string[] ss = new string[3];
            ss[0] = "\u0100";
            ss[2] = "\u0100";
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException56()
        {
            Type type;
            string[] ss = new string[3];
            ss[0] = "\u0100";
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException94()
        {
            Type type;
            string[] ss = new string[4];
            ss[0] = "\u0100";
            ss[1] = "\u0100";
            ss[2] = "\u0100";
            ss[3] = "\u0100";
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException921()
        {
            Type type;
            string[] ss = new string[5];
            ss[0] = "\u0100";
            ss[2] = "\u0100";
            ss[3] = "\u0100";
            ss[4] = "\u0100";
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException315()
        {
            Type type;
            string[] ss = new string[0];
            type = this.ResolveType("\u0100\0", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException192()
        {
            Type type;
            string[] ss = new string[0];
            type = this.ResolveType("\u0100\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException118()
        {
            Type type;
            string[] ss = new string[9];
            ss[0] = "\u0100";
            ss[1] = "\u0100";
            ss[2] = "\u0100";
            ss[3] = "\u0100";
            ss[5] = "\u0100";
            ss[8] = "\u0100";
            type = this.ResolveType("\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException715()
        {
            Type type;
            string[] ss = new string[0];
            type = this.ResolveType("\u0100\u0100\0", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException334()
        {
            Type type;
            string[] ss = new string[0];
            type = this.ResolveType("\u0100\u0100\u0100", (IEnumerable<string>)ss);
        }
        [TestMethod]
        public void ResolveTypeThrowsTemplateCompileException792()
        {
            Type type;
            string[] ss = new string[0];
            type = this.ResolveType("\u0100\u0100\u0100\u0100", (IEnumerable<string>)ss);
        }
    }
}
