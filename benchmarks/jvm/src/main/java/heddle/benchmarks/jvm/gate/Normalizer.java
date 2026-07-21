package heddle.benchmarks.jvm.gate;

import java.nio.ByteBuffer;
import java.nio.CharBuffer;
import java.nio.charset.CharacterCodingException;
import java.nio.charset.CharsetDecoder;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;

/**
 * Contract-literal implementation of the parity-contract-v2 normalization pipeline
 * (N1-N5 plus the N3b comparison-time whitespace strip). Nothing outside the closed list
 * is applied; the whitespace set is the explicit six-character set
 * {TAB, LF, VT, FF, CR, SPACE} - never a language {@code \s} class.
 * Spec: docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md
 * and phase-3-jvm/harness-and-jmh.md (gate runner implementation contract, D11).
 */
public final class Normalizer {

    private Normalizer() {
    }

    /** The six-character whitespace set. Deliberately not {@code Character.isWhitespace}. */
    static boolean isWs(char c) {
        return c == '\t' || c == '\n' || c == 0x0B || c == '\f' || c == '\r' || c == ' ';
    }

    /**
     * N1: strict UTF-8 decode; invalid UTF-8 is a gate failure. A leading U+FEFF (BOM) is
     * kept - it survives to the comparison and fails it.
     */
    public static String decodeUtf8Strict(byte[] bytes) {
        CharsetDecoder decoder = StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT);
        try {
            CharBuffer decoded = decoder.decode(ByteBuffer.wrap(bytes));
            return decoded.toString();
        } catch (CharacterCodingException e) {
            throw new IllegalStateException("N1: invalid UTF-8 in candidate/corpus bytes", e);
        }
    }

    /**
     * Applies N2, N3, N4 and (encoded suite only) N5 in contract order. N1 is applied at
     * byte-decode time ({@link #decodeUtf8Strict}); N3b is a comparison-time projection
     * applied separately via {@link #strip}.
     */
    public static String normalize(String text, boolean encodedSuite) {
        String s = n2(text);
        s = n3(s);
        s = n4(s);
        if (encodedSuite) {
            s = n5(s);
        }
        return s;
    }

    /** N2: replace every CRLF with LF, then every remaining CR with LF. */
    static String n2(String s) {
        return s.replace("\r\n", "\n").replace('\r', '\n');
    }

    /**
     * N3: single left-to-right scan collapsing every run of six-set whitespace between
     * {@code >} and {@code <} to nothing. Replacements cannot create new matches
     * (the replacement {@code ><} contains no whitespace), so one pass suffices.
     */
    static String n3(String s) {
        StringBuilder sb = new StringBuilder(s.length());
        int i = 0;
        int n = s.length();
        while (i < n) {
            char c = s.charAt(i);
            sb.append(c);
            if (c == '>') {
                int j = i + 1;
                while (j < n && isWs(s.charAt(j))) {
                    j++;
                }
                if (j > i + 1 && j < n && s.charAt(j) == '<') {
                    i = j;      // skip the whitespace run; '<' appended on the next iteration
                    continue;
                }
            }
            i++;
        }
        return sb.toString();
    }

    /** N4: trim leading/trailing characters from the six-character whitespace set. */
    static String n4(String s) {
        int start = 0;
        int end = s.length();
        while (start < end && isWs(s.charAt(start))) {
            start++;
        }
        while (end > start && isWs(s.charAt(end - 1))) {
            end--;
        }
        return s.substring(start, end);
    }

    /**
     * N3b (2026-07-20 maintainer step): remove every run of six-set whitespace anywhere,
     * to nothing (not to a space). Applied symmetrically at comparison time to both the
     * normalized candidate and the loaded golden, and to every verifier needle - never
     * baked into the stored oracle.
     */
    public static String strip(String s) {
        StringBuilder sb = new StringBuilder(s.length());
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (!isWs(c)) {
                sb.append(c);
            }
        }
        return sb.toString();
    }

    /**
     * N5, encoded suite only: the contract's closed entity-canonicalization table. Named
     * entities match case-sensitively; numeric references match with any number of leading
     * zeros, case-insensitive hex digits and {@code x}. Single left-to-right scan;
     * replacements are non-overlapping and never rescanned. Only spellings of the five
     * markup-significant characters are canonicalized.
     */
    static String n5(String s) {
        StringBuilder sb = new StringBuilder(s.length());
        int i = 0;
        int n = s.length();
        while (i < n) {
            char c = s.charAt(i);
            if (c == '&') {
                int consumed = tryEntity(s, i, sb);
                if (consumed > 0) {
                    i += consumed;
                    continue;
                }
            }
            sb.append(c);
            i++;
        }
        return sb.toString();
    }

    /**
     * Attempts to match one recognized spelling starting at {@code s.charAt(at) == '&'}.
     * On a match appends the canonical spelling to {@code out} and returns the number of
     * input chars consumed; returns 0 otherwise.
     */
    private static int tryEntity(String s, int at, StringBuilder out) {
        // Named entities - case-sensitive.
        String[][] named = {
                {"&amp;", "&amp;"},
                {"&lt;", "&lt;"},
                {"&gt;", "&gt;"},
                {"&quot;", "&quot;"},
                {"&apos;", "&#39;"},
        };
        for (String[] e : named) {
            if (s.startsWith(e[0], at)) {
                out.append(e[1]);
                return e[0].length();
            }
        }
        // Numeric references: &#123; or &#x7B; - any leading zeros, case-insensitive hex/x.
        if (at + 2 >= s.length() || s.charAt(at + 1) != '#') {
            return 0;
        }
        int p = at + 2;
        boolean hex = false;
        if (p < s.length() && (s.charAt(p) == 'x' || s.charAt(p) == 'X')) {
            hex = true;
            p++;
        }
        int digitsStart = p;
        long value = 0;
        boolean overflow = false;
        while (p < s.length()) {
            char d = s.charAt(p);
            int dv;
            if (d >= '0' && d <= '9') {
                dv = d - '0';
            } else if (hex && d >= 'a' && d <= 'f') {
                dv = d - 'a' + 10;
            } else if (hex && d >= 'A' && d <= 'F') {
                dv = d - 'A' + 10;
            } else {
                break;
            }
            if (!overflow) {
                value = value * (hex ? 16 : 10) + dv;
                if (value > 0x10FFFF) {
                    overflow = true;
                }
            }
            p++;
        }
        if (p == digitsStart || p >= s.length() || s.charAt(p) != ';' || overflow) {
            return 0;
        }
        String canonical = switch ((int) value) {
            case 0x26 -> "&amp;";
            case 0x3C -> "&lt;";
            case 0x3E -> "&gt;";
            case 0x22 -> "&quot;";
            case 0x27 -> "&#39;";
            default -> null;
        };
        if (canonical == null) {
            return 0;
        }
        out.append(canonical);
        return p + 1 - at;
    }
}
