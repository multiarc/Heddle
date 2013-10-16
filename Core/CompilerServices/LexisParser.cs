using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Templates.Core.Data;

namespace Templates.Core.CompilerServices {
    /// <summary>
    /// Lexical analyzer for engine
    /// </summary>
    internal static class LexisParser {
        private static readonly Regex LexicalExpression = new Regex
            (string.Format
                 (CultureInfo.InvariantCulture, @"(\{0}\{4}|\{4}\{1}|\{0}|\{1}|\{2}|\{3}|\s+|\{5}|[^\{0}\{4}\{1}\{2}\{3}\s\{5}]+)",
                  ParserConfiguration.StartExtensionsBlock, ParserConfiguration.EndExtensionsBlock, ParserConfiguration.StartParameter,
                  ParserConfiguration.EndParameter, ParserConfiguration.SequenceLiteral, ParserConfiguration.ExtensionDelimeter),
             RegexOptions.Singleline | RegexOptions.Compiled);

        // (((?'Start'<%)(?(%)(?<!<)%[^>]|(?(<)(<[^%])|(?(>)(?<!%)>|.)))*?)+((?'End-Start'%>)(?(%)(?<!<)%[^>]|(?(<)(<[^%])|(?(>)(?<!%)>|.)))*?)+)(?(Start)(?!))
        private static readonly Regex TemplateBlockExpression = new Regex
            (string.Format
                 (CultureInfo.InvariantCulture,
                  @"(((?'Start'{0})(?({2})(?<!{3})%[^{4}]|(?({3})({3}[^{2}])|(?({4})(?<!{2}){4}|.)))*?)+((?'End-Start'{1})(?({2})(?<!{3})%[^{4}]|(?({3})({3}[^{2}])|(?({4})(?<!{2}){4}|.)))*?)+)(?(Start)(?!))",
                  ParserConfiguration.StartTTL, ParserConfiguration.EndTTL, ParserConfiguration.SequenceLiteral,
                  ParserConfiguration.StartExtensionsBlock, ParserConfiguration.EndExtensionsBlock), RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Creates list of tokens from source template
        /// </summary>
        /// <param name="document">Source template represented as large string</param>
        /// <returns>Tokens list, also available in the object in Tokens Property</returns>
        public static IEnumerable<Token> Parse (string document)
        {
            if (string.IsNullOrWhiteSpace(document))
                throw new ArgumentException();

            MatchCollection matches = LexicalExpression.Matches(document);
            return from Match match in matches
                   select new Token
                   {
                       CapturedString = match.Value,
                       Length = match.Length,
                       StartIndex = match.Index,
                       Type = DetermineTokenType(match.Value)
                   };
        }

        public static IEnumerable<Token> GetTemplateBlocks (string document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            MatchCollection matches = TemplateBlockExpression.Matches(document);
            return from Match match in matches
                   select new Token
                   {
                       CapturedString = match.Value,
                       Length = match.Length,
                       StartIndex = match.Index,
                       Type = TokenType.TemplateBlock
                   };
        }

        private static TokenType DetermineTokenType (string capturedFastString)
        {
            switch (capturedFastString) {
                case ParserConfiguration.StartTTL:
                    return TokenType.StartTTL;
                case ParserConfiguration.EndTTL:
                    return TokenType.EndTTL;
                case ParserConfiguration.StartExtensionsBlock:
                    return TokenType.StartExtensionsBlock;
                case ParserConfiguration.EndExtensionsBlock:
                    return TokenType.EndExtensionsBlock;
                case ParserConfiguration.StartParameter:
                    return TokenType.StartParameter;
                case ParserConfiguration.EndParameter:
                    return TokenType.EndParameter;
                case ParserConfiguration.ExtensionDelimeter:
                    return TokenType.ExtensionDelimeter;
            }
            if (ParserHelper.IsSpace(capturedFastString))
                return TokenType.Space;
            return ParserHelper.IsValidIdentifier(capturedFastString) ? TokenType.ValidIdentifier : TokenType.Other;
        }
    }
}