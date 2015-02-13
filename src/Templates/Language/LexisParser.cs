using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Templates.Data;
using Templates.Strings;

namespace Templates.Language {
    /// <summary>
    /// Lexical analyzer for engine
    /// </summary>
    public static class LexisParser {
        private static readonly Regex LexicalExpression = new Regex
            (string.Format
                (CultureInfo.InvariantCulture, @"({0}|{1}|{2}|{3}|\{4}|\{5}|{6}|\s+|(?>.+))", ParserConfiguration.StartTtl,
                    ParserConfiguration.EndTtl, ParserConfiguration.StartExtensionsBlock,
                    ParserConfiguration.EndExtensionsBlock, ParserConfiguration.StartParameter,
                    ParserConfiguration.EndParameter, ParserConfiguration.ExtensionDelimeter), RegexOptions.Singleline | RegexOptions.Compiled);
        //private static readonly Regex LexicalExpression = new Regex
        //    (string.Format
        //         (CultureInfo.InvariantCulture, @"(\{0}\{4}|\{4}\{1}|\{0}|\{1}|\{2}|\{3}|\s+|\{5}|[^\{0}\{4}\{1}\{2}\{3}\s\{5}]+)",
        //          ParserConfiguration.StartExtensionsBlock, ParserConfiguration.EndExtensionsBlock, ParserConfiguration.StartParameter,
        //          ParserConfiguration.EndParameter, ParserConfiguration.SequenceLiteral, ParserConfiguration.ExtensionDelimeter),
        //     RegexOptions.Singleline | RegexOptions.Compiled);

        // (((?'Start'<%)(?(%)(?<!<)%[^>]|(?(<)(<[^%])|(?(>)(?<!%)>|.)))*?)+((?'End-Start'%>)(?(%)(?<!<)%[^>]|(?(<)(<[^%])|(?(>)(?<!%)>|.)))*?)+)(?(Start)(?!))
        //private static readonly Regex TemplateBlockExpression = new Regex
        //    (string.Format
        //         (CultureInfo.InvariantCulture,
        //          @"(((?'Start'{0})(?({2})(?<!{3})%[^{4}]|(?({3})({3}[^{2}])|(?({4})(?<!{2}){4}|.)))*?)+((?'End-Start'{1})(?({2})(?<!{3})%[^{4}]|(?({3})({3}[^{2}])|(?({4})(?<!{2}){4}|.)))*?)+)(?(Start)(?!))",
        //          ParserConfiguration.StartTtl, ParserConfiguration.EndTtl, ParserConfiguration.SequenceLiteral,
        //          ParserConfiguration.StartExtensionsBlock, ParserConfiguration.EndExtensionsBlock), RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Creates list of tokens from source template
        /// </summary>
        /// <param name="document">Source template represented as large string</param>
        /// <returns>Tokens list, also available in the object in Tokens Property</returns>
        public static IEnumerable<Token> TokenizeAlt(string document) {
            if (string.IsNullOrEmpty(document))
                return Enumerable.Empty<Token>();
            MatchCollection matches = LexicalExpression.Matches(document);
            return from Match match in matches
                   select new Token {
                       CapturedString = match.Value,
                       Length = match.Length,
                       StartIndex = match.Index,
                       Type = DetermineTokenType(match.Value)
                   };
        }

        public static IEnumerable<Token> Tokenize(string document)
        {
            if (string.IsNullOrEmpty(document))
                yield break;
            int start = 0;
            int maxLength = document.Length;
            ExStringBuilder tokenBuilder = new ExStringBuilder();
            while (start < maxLength)
            {
                var tokenCap = document.Substring(start, start + 1 < maxLength ? 2 : 1);
                switch (tokenCap)
                {
                    case ParserConfiguration.StartTtl:
                    case ParserConfiguration.EndTtl:
                        if (tokenBuilder.Length > 0)
                        {
                            yield return
                                new Token
                                {
                                    CapturedString = tokenBuilder.ToString(),
                                    Length = tokenBuilder.Length,
                                    StartIndex = start - tokenBuilder.Length + 1,
                                    Type = DetermineTokenType(tokenBuilder.ToString())
                                };
                            tokenBuilder.Clear();
                        }
                        yield return
                            new Token
                            {
                                CapturedString = tokenCap,
                                Length = tokenCap.Length,
                                StartIndex = start,
                                Type = DetermineTokenType(tokenCap)
                            };
                        start += 2;
                        break;
                    default:
                        string symbol = tokenCap.Substring(0, 1);
                        switch (symbol)
                        {
                            case ParserConfiguration.StartExtensionsBlock:
                            case ParserConfiguration.EndExtensionsBlock:
                            case ParserConfiguration.StartParameter:
                            case ParserConfiguration.EndParameter:
                            case ParserConfiguration.ExtensionDelimeter:
                            case "\t":
                            case "\r":
                            case "\n":
                            case " ":
                            case "\u00A0":
                                if (tokenBuilder.Length > 0)
                                {
                                    yield return
                                        new Token
                                        {
                                            CapturedString = tokenBuilder.ToString(),
                                            Length = tokenBuilder.Length,
                                            StartIndex = start - tokenBuilder.Length + 1,
                                            Type = DetermineTokenType(tokenBuilder.ToString())
                                        };
                                    tokenBuilder.Clear();
                                }
                                yield return
                                    new Token
                                    {
                                        CapturedString = symbol,
                                        Length = symbol.Length,
                                        StartIndex = start,
                                        Type = DetermineTokenType(symbol)
                                    };
                                break;
                            default:
                                tokenBuilder.Append(symbol);
                                break;
                        }
                        start++;
                        break;
                }
            }
            if (tokenBuilder.Length > 0)
            {
                yield return
                    new Token
                    {
                        CapturedString = tokenBuilder.ToString(),
                        Length = tokenBuilder.Length,
                        StartIndex = start - tokenBuilder.Length + 1,
                        Type = DetermineTokenType(tokenBuilder.ToString())
                    };
                tokenBuilder.Clear();
            }
        }

        //public static IEnumerable<Token> GetTemplateBlocks (string document)
        //{
        //    if (document == null)
        //        throw new ArgumentNullException("document");

        //    MatchCollection matches = TemplateBlockExpression.Matches(document);
        //    return from Match match in matches
        //           select new Token
        //           {
        //               CapturedString = match.Value,
        //               Length = match.Length,
        //               StartIndex = match.Index,
        //               Type = TokenType.TemplateBlock
        //           };
        //}

        private static TokenType DetermineTokenType(string capturedFastString) {
            switch (capturedFastString) {
            case ParserConfiguration.StartTtl:
                return TokenType.StartTtl;
            case ParserConfiguration.EndTtl:
                return TokenType.EndTtl;
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