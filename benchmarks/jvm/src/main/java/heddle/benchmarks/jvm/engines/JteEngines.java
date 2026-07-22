package heddle.benchmarks.jvm.engines;

import gg.jte.ContentType;
import gg.jte.TemplateEngine;
import gg.jte.TemplateException;
import gg.jte.output.StringOutput;
import heddle.benchmarks.jvm.jte.FiveEntityHtmlOutput;

/**
 * The two precompiled JTE engines plus render helpers (spec D3/D4,
 * harness-and-jmh.md &sect;Engine construction). AOT posture: the jte-maven-plugin
 * {@code generate} goal compiles templates into the jar; at runtime the engine is
 * {@code TemplateEngine.createPrecompiled(null, contentType, null, packageName)} - class
 * loader loading, no runtime javac. D3's documented fallback (if the null class directory
 * were rejected): {@code createPrecompiled(ContentType)} semantics; taking it is recorded
 * via {@link #fallbackUsed()}.
 *
 * Cells whose templates are not yet authored (WI3/WI4) surface as
 * {@link MissingTemplate} so the gate CLI can report them cleanly.
 */
public final class JteEngines {

    /** Render failed because the template class is absent (template WI not landed yet). */
    public static final class MissingTemplate extends RuntimeException {
        public MissingTemplate(String message, Throwable cause) {
            super(message, cause);
        }
    }

    private static volatile TemplateEngine plain;
    private static volatile TemplateEngine html;
    private static volatile boolean fallbackUsed;

    private JteEngines() {
    }

    /** Raw suite engine: {@code ContentType.Plain}, package {@code heddle.jte.gen.plain}. */
    public static TemplateEngine plain() {
        TemplateEngine e = plain;
        if (e == null) {
            synchronized (JteEngines.class) {
                e = plain;
                if (e == null) {
                    plain = e = create(ContentType.Plain, "heddle.jte.gen.plain");
                }
            }
        }
        return e;
    }

    /** Encoded suite engine: {@code ContentType.Html}, package {@code heddle.jte.gen.html}. */
    public static TemplateEngine html() {
        TemplateEngine e = html;
        if (e == null) {
            synchronized (JteEngines.class) {
                e = html;
                if (e == null) {
                    html = e = create(ContentType.Html, "heddle.jte.gen.html");
                }
            }
        }
        return e;
    }

    /** True when D3's documented fallback path had to be taken (report-worthy). */
    public static boolean fallbackUsed() {
        return fallbackUsed;
    }

    private static TemplateEngine create(ContentType contentType, String packageName) {
        try {
            // D3 primary: null class directory => application-class-loader loading.
            return TemplateEngine.createPrecompiled(null, contentType, null, packageName);
        } catch (RuntimeException primaryFailure) {
            // D3 documented fallback: createPrecompiled(ContentType) semantics
            // (default package name). Recorded so the run report can disclose it.
            fallbackUsed = true;
            return TemplateEngine.createPrecompiled(contentType);
        }
    }

    /** One raw-suite render: plain engine into a fresh {@link StringOutput}. */
    public static String renderPlain(String template, Object model) {
        StringOutput out = new StringOutput();
        render(plain(), template, model, out);
        return out.toString();
    }

    /** One controlled encoded render: Html engine into the D4 custom output. */
    public static String renderHtmlControlled(String template, Object model) {
        FiveEntityHtmlOutput out = new FiveEntityHtmlOutput();
        render(html(), template, model, out);
        return out.toString();
    }

    /**
     * One idiomatic encoded render: Html engine into a plain {@link StringOutput} - the
     * engine wraps it in the stock {@code OwaspHtmlTemplateOutput} itself.
     */
    public static String renderHtmlIdiomatic(String template, Object model) {
        StringOutput out = new StringOutput();
        render(html(), template, model, out);
        return out.toString();
    }

    private static void render(TemplateEngine engine, String template, Object model,
                               gg.jte.TemplateOutput out) {
        try {
            engine.render(template, model, out);
        } catch (TemplateException e) {
            throw new MissingTemplate("jte template '" + template
                    + "' not renderable (not authored yet, or broken): " + e.getMessage(), e);
        }
    }
}
