package heddle.benchmarks.jvm.jte;

import gg.jte.html.HtmlTemplateOutput;
import gg.jte.output.StringOutput;

import java.util.Locale;

/**
 * D4: the JTE controlled encoded suite renders through this custom
 * {@link gg.jte.html.HtmlTemplateOutput} instead of the stock OWASP output.
 * {@code writeUserContent(String)} escapes exactly the five markup-significant characters
 * to the canonical Phase 1 spellings ({@code &amp; &lt; &gt; &quot; &#39;}) in both
 * tag-body and attribute contexts; all {@code writeContent} overloads (inherited from
 * {@link StringOutput}) append verbatim; primitive {@code writeUserContent} overloads
 * append their {@code String.valueOf} (never escapable). {@code setContext} records the
 * context and throws {@link IllegalStateException} on a {@code script}-tag or
 * {@code on*}-attribute JavaScript context - a tripwire, not a path: no such context
 * exists in the corpus templates.
 * {@code TemplateEngine.checkOutput} uses a caller-supplied {@code HtmlTemplateOutput}
 * as-is (verified at source, spec Assumed state), so this is a supported engine seam.
 * Spec: docs/spec/cross-stack-benchmarks/phase-3-jvm/README.md D4.
 */
public final class FiveEntityHtmlOutput extends StringOutput implements HtmlTemplateOutput {

    private String tagName;
    private String attributeName;

    @Override
    public void setContext(String tagName, String attributeName) {
        if (tagName != null && "script".equals(tagName.toLowerCase(Locale.ROOT))) {
            throw new IllegalStateException("Unexpected JavaScript escaping context "
                    + tagName + "/" + attributeName
                    + " — controlled templates must not create one");
        }
        if (attributeName != null
                && attributeName.toLowerCase(Locale.ROOT).startsWith("on")) {
            throw new IllegalStateException("Unexpected JavaScript escaping context "
                    + tagName + "/" + attributeName
                    + " — controlled templates must not create one");
        }
        this.tagName = tagName;
        this.attributeName = attributeName;
    }

    @Override
    public void writeUserContent(String value) {
        if (value != null) {
            writeContent(escape(value));
        }
    }

    /**
     * Escapes exactly {@code & < > " '} to {@code &amp; &lt; &gt; &quot; &#39;};
     * every other character (including BMP >= U+0100) passes through untouched.
     */
    public static String escape(String value) {
        StringBuilder sb = null;
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            String replacement = switch (c) {
                case '&' -> "&amp;";
                case '<' -> "&lt;";
                case '>' -> "&gt;";
                case '"' -> "&quot;";
                case '\'' -> "&#39;";
                default -> null;
            };
            if (replacement == null) {
                if (sb != null) {
                    sb.append(c);
                }
            } else {
                if (sb == null) {
                    sb = new StringBuilder(value.length() + 16);
                    sb.append(value, 0, i);
                }
                sb.append(replacement);
            }
        }
        return sb == null ? value : sb.toString();
    }
}
