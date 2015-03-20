using Templates.Exceptions;
using Antlr4.Runtime;
using Templates.Strings.Core;
// <copyright file="ParseContextTest.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Templates.Language;

namespace Templates.Language
{
    [TestClass]
    [PexClass(typeof(ParseContext))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class ParseContextTest
    {
        [PexMethod]
        internal ParseContext Constructor(ParseContext previous, int offset) {
            ParseContext target = new ParseContext(previous, offset);
            return target;
            // TODO: add assertions to method ParseContextTest.Constructor(ParseContext, Int32)
        }
        [PexMethod(Timeout = 240)]
        internal DefinitionItem CreateDefinition([PexAssumeUnderTest]ParseContext target, TtlParser.DefContext context) {
            DefinitionItem result = target.CreateDefinition(context);
            return result;
            // TODO: add assertions to method ParseContextTest.CreateDefinition(ParseContext, DefContext)
        }
        [PexMethod]
        [PexAllowedException(typeof(TemplateParseException))]
        internal RawOutputItem CreateRawOutputItem([PexAssumeUnderTest]ParseContext target, TtlParser.RawContext context) {
            RawOutputItem result = target.CreateRawOutputItem(context);
            return result;
            // TODO: add assertions to method ParseContextTest.CreateRawOutputItem(ParseContext, RawContext)
        }
        [PexMethod]
        internal OutputChain CreateOutputChain([PexAssumeUnderTest]ParseContext target, TtlParser.OutblockContext context) {
            OutputChain result = target.CreateOutputChain(context);
            return result;
            // TODO: add assertions to method ParseContextTest.CreateOutputChain(ParseContext, OutblockContext)
        }
        [PexMethod]
        internal BlockPosition GetBlockPosition([PexAssumeUnderTest]ParseContext target, ParserRuleContext context) {
            BlockPosition result = target.GetBlockPosition(context);
            return result;
            // TODO: add assertions to method ParseContextTest.GetBlockPosition(ParseContext, ParserRuleContext)
        }
    }
}
