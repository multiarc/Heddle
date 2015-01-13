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

        public static string Encode (string input)
        {
            if (!string.IsNullOrEmpty(input)) {
                unsafe {
                    fixed (char* src = input) {
                        return FastEncode(input, src);
                    }
                }
            }
            return string.Empty;
        }

        private static unsafe string FastEncode (string input, char* src)
        {
            int len = input.Length;
            var replacements = new SmartList<Replacement>();
            for (int i = 0; i < len; i++) {
                string replacement = LookupTable[src[i]];
                if (replacement != null) {
                    replacements.Add
                        (new Replacement
                        {
                            Position = new Position(i, 1),
                            ReplacementValue = replacement
                        });
                }
            }
            return replacements.Length > 0 ? FastStringBuilder.BulkReplace(replacements, input) : input;
        }
    }
}