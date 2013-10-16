using System;
using System.Text.RegularExpressions;

namespace Templates.Core.CompilerServices {
    /// <summary>
    /// General rules and other helpers for Syntax or Lexical parser
    /// </summary>
    internal static class ParserHelper {
        private static readonly Regex IdExpression = new Regex
            (@"^@?[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]+?$",
             RegexOptions.Compiled);

        private static readonly Regex SpaceExpression = new Regex(@"^[\s\r\n]+$", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Checks for valid space sequence
        /// </summary>
        public static bool IsSpace (string rawChunk)
        {
            if (rawChunk == null)
                throw new ArgumentNullException("rawChunk");

            return SpaceExpression.IsMatch(rawChunk);
        }

        /// <summary>
        /// Checks for valid identifier
        /// </summary>
        public static bool IsValidIdentifier (string rawChunk)
        {
            if (rawChunk == null)
                throw new ArgumentNullException("rawChunk");

            return IdExpression.IsMatch(rawChunk);
        }
    }
}