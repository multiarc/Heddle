// <copyright file="BlockPositionTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Strings.Core;

namespace Templates.Strings.Core
{
    [TestClass]
    [PexClass(typeof(BlockPosition))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class BlockPositionTest
    {
        [PexMethod]
        [PexAllowedException(typeof(ArgumentException))]
        public BlockPosition Constructor(int startIndex, int length) {
            BlockPosition target = new BlockPosition(startIndex, length);
            return target;
            // TODO: add assertions to method BlockPositionTest.Constructor(Int32, Int32)
        }
    }
}
