using System.Collections.Generic;
using System.Net;
using Templates.Strings.Core;

namespace Templates.Strings.Web {
    public static class HtmlEncode {
        private const string Quot = "&quot;"; // 34
        private const string Amp = "&amp;"; // 38
        private const string Apos = "&apos;"; // 39
        private const string Lt = "&lt;"; // 60
        private const string Gt = "&gt;"; // 62
        private const string Nbsp = "&nbsp;"; // 160
        private static readonly string[] LookupTable;

        static HtmlEncode ()
        {
            LookupTable = new string[char.MaxValue + 1];
            LookupTable[34] = Quot;
            LookupTable[38] = Amp;
            LookupTable[39] = Apos;
            LookupTable[60] = Lt;
            LookupTable[62] = Gt;
            LookupTable[160] = Nbsp;
        }

        public static string EncodeAlt(string input)
        {
            if (!string.IsNullOrEmpty(input)) {
                unsafe {
                    fixed (char* src = input) {
                        int len = input.Length;
                        var replacements = new List<Replacement>();
                        for (int i = 0; i < len; i++) {
                            string replacement = LookupTable[src[i]];
                            if (replacement != null) {
                                replacements.Add
                                    (new Replacement {
                                        BlockPosition = new BlockPosition(i, 1),
                                        ReplacementValue = replacement
                                    });
                            }
                        }
                        return replacements.Count > 0
                            ? ExStringBuilder.BulkReplace(replacements, input)
                            : input;
                    }
                }
            }
            return string.Empty;
        }

        public static string Encode(string input)
        {
            return WebUtility.HtmlEncode(input);
        }
    }
}