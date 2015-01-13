using System;
using System.Collections.Generic;

namespace Templates.Core.Data {
    /// <summary>
    /// System Template Configuration parser.Loads string defined in configuration file and creates apropriate objects as representation of configuration.
    /// </summary>
    public static class SystemPatternStrings {
        private static readonly Dictionary<string, string> SystemStrings = ReadSystemStrings();

        /// <summary>
        /// Gets Dictinary with all parsed system string names and apropriate values
        /// </summary>
        /// <returns></returns>
        private static Dictionary<string, string> ReadSystemStrings ()
        {
            return new Dictionary<string, string>
            {
                {
                    @"\", "<%"
                },
                {
                    "/", "%>"
                },
                {
                    "[", "{"
                },
                {
                    "]", "}"
                }
            };
        }

        public static string ReplaceAll (string input)
        {
            if (input == null)
                throw new ArgumentNullException("input");

            string result = input;
            foreach (var systemString in SystemStrings)
                result = result.Replace(systemString.Key, systemString.Value);
            return result;
        }
    }
}