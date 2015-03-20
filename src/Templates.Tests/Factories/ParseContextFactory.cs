using Templates.Language;
// <copyright file="ParseContextFactory.cs" company="Aliaksandr Kukrash">Copyright © 2012 Aliaksandr Kukrash</copyright>

using System;
using Microsoft.Pex.Framework;

namespace Templates.Language
{
    /// <summary>A factory for Templates.Language.ParseContext instances</summary>
    public static partial class ParseContextFactory {
        /// <summary>A factory for Templates.Language.ParseContext instances</summary>
        [PexFactoryMethod(typeof(ParseContext))]
        public static ParseContext Create(
            ParseContext previous_parseContext1,
            int offset_i,
            bool value_b,
            bool value_b1
        ) {
            ParseContext parseContext = new ParseContext(previous_parseContext1, offset_i);
            parseContext.DefenitionsOnly = value_b;
            parseContext.InDefinition = value_b1;
            return parseContext;

            // TODO: Edit factory method of ParseContext
            // This method should be able to configure the object in all possible ways.
            // Add as many parameters as needed,
            // and assign their values to each field by using the API.
        }
    }
}
