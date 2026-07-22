package heddle.benchmarks.jvm.engines;

import heddle.benchmarks.jvm.model.Models;
import org.thymeleaf.TemplateEngine;
import org.thymeleaf.context.Context;
import org.thymeleaf.exceptions.TemplateInputException;
import org.thymeleaf.templatemode.TemplateMode;
import org.thymeleaf.templateresolver.ClassLoaderTemplateResolver;
import org.thymeleaf.templateresolver.StringTemplateResolver;

import java.io.StringWriter;
import java.util.HashMap;
import java.util.Map;

/**
 * The two Thymeleaf engines plus reused per-workload contexts (spec D2): standalone
 * 3.1.5.RELEASE, {@code ClassLoaderTemplateResolver} (prefix {@code thymeleaf/}, suffix
 * {@code .html}, {@code TemplateMode.HTML}, cacheable), one render =
 * {@code engine.process("<track>/<workload>", context, writer)} into a fresh
 * {@link StringWriter}; the {@link Context} is built once per workload and reused.
 * No Spring, no servlet context.
 *
 * Also hosts the D5 escaper-probe helpers: a {@link StringTemplateResolver}-backed engine
 * (probe only - the benchmark engines never use it, per D2's rejected alternatives) that
 * exercises the real Thymeleaf text-inlining and attribute escaping paths without needing
 * the WI2 template resources.
 */
public final class ThymeleafEngines {

    private static volatile TemplateEngine controlled;
    private static volatile TemplateEngine idiomatic;
    private static volatile TemplateEngine probe;
    private static final Map<String, Context> CONTEXTS = new HashMap<>();

    private ThymeleafEngines() {
    }

    /** One engine per track (D2). */
    public static TemplateEngine forTrack(String track) {
        if ("controlled".equals(track)) {
            TemplateEngine e = controlled;
            if (e == null) {
                synchronized (ThymeleafEngines.class) {
                    e = controlled;
                    if (e == null) {
                        controlled = e = createEngine();
                    }
                }
            }
            return e;
        }
        if ("idiomatic".equals(track)) {
            TemplateEngine e = idiomatic;
            if (e == null) {
                synchronized (ThymeleafEngines.class) {
                    e = idiomatic;
                    if (e == null) {
                        idiomatic = e = createEngine();
                    }
                }
            }
            return e;
        }
        throw new IllegalArgumentException("Unknown track '" + track + "'");
    }

    private static TemplateEngine createEngine() {
        ClassLoaderTemplateResolver resolver = new ClassLoaderTemplateResolver();
        resolver.setPrefix("thymeleaf/");
        resolver.setSuffix(".html");
        resolver.setTemplateMode(TemplateMode.HTML);
        resolver.setCacheable(true);
        TemplateEngine engine = new TemplateEngine();
        engine.setTemplateResolver(resolver);
        return engine;
    }

    /** The per-workload variables (construct-mapping.md), built once and reused. */
    public static synchronized Context context(String workload) {
        Context ctx = CONTEXTS.get(workload);
        if (ctx != null) {
            return ctx;
        }
        ctx = new Context();
        switch (workload) {
            case "composed-page" -> {
                Models.ComposedModel m = Models.composed();
                ctx.setVariable("sections", m.getSections());
                ctx.setVariable("comps", m.getComps());
                ctx.setVariable("areas", m.getAreas());
                ctx.setVariable("areaNames", m.getAreaNames());
            }
            case "trivial-substitution" -> ctx.setVariable("m", Models.SUBSTITUTION);
            case "large-loop" -> ctx.setVariable("items", Models.LOOP_ROWS);
            case "mixed-page" -> ctx.setVariable("m", Models.MIXED);
            case "conditional-heavy" -> ctx.setVariable("rows", Models.CONDITIONAL_ROWS);
            case "fragment-heavy" -> ctx.setVariable("items", Models.FRAGMENT_ROWS);
            case "fortunes-encoded" -> ctx.setVariable("rows", Models.FORTUNE_ROWS);
            case "encoded-loop" -> ctx.setVariable("items", Models.ENCODED_ITEMS);
            default -> throw new IllegalArgumentException("Unknown workload '" + workload + "'");
        }
        CONTEXTS.put(workload, ctx);
        return ctx;
    }

    /** One render (D2): fresh StringWriter, cached template, reused context. */
    public static String render(String track, String workload) {
        StringWriter writer = new StringWriter();
        try {
            forTrack(track).process(track + "/" + workload, context(workload), writer);
        } catch (TemplateInputException e) {
            throw new JteEngines.MissingTemplate("thymeleaf template '" + track + "/" + workload
                    + "' not resolvable (not authored yet, or broken): " + e.getMessage(), e);
        }
        return writer.toString();
    }

    // ---- D5 escaper probe helpers ------------------------------------------------------

    private static TemplateEngine probeEngine() {
        TemplateEngine e = probe;
        if (e == null) {
            synchronized (ThymeleafEngines.class) {
                e = probe;
                if (e == null) {
                    StringTemplateResolver resolver = new StringTemplateResolver();
                    resolver.setTemplateMode(TemplateMode.HTML);
                    TemplateEngine engine = new TemplateEngine();
                    engine.setTemplateResolver(resolver);
                    probe = e = engine;
                }
            }
        }
        return e;
    }

    /** Thymeleaf default text escaping: escaped inlining {@code [[...]]} in a tag body. */
    public static String escapeTextViaEngine(String value) {
        Context ctx = new Context();
        ctx.setVariable("v", value);
        StringWriter writer = new StringWriter();
        probeEngine().process("<td>[[${v}]]</td>", ctx, writer);
        String out = writer.toString();
        int start = out.indexOf("<td>") + 4;
        int end = out.lastIndexOf("</td>");
        if (start < 4 || end < start) {
            throw new IllegalStateException("Unexpected probe output: " + out);
        }
        return out.substring(start, end);
    }

    /** Thymeleaf attribute escaping: {@code th:attr} on a double-quoted attribute value. */
    public static String escapeAttributeViaEngine(String value) {
        Context ctx = new Context();
        ctx.setVariable("v", value);
        StringWriter writer = new StringWriter();
        probeEngine().process("<td th:attr=\"data-tag=${v}\"></td>", ctx, writer);
        String out = writer.toString();
        String open = "data-tag=\"";
        int start = out.indexOf(open);
        int end = out.lastIndexOf('"');
        if (start < 0 || end <= start + open.length() - 1) {
            throw new IllegalStateException("Unexpected probe output: " + out);
        }
        return out.substring(start + open.length(), end);
    }
}
