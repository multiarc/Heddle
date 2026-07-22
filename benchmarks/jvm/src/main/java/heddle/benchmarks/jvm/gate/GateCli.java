package heddle.benchmarks.jvm.gate;

import gg.jte.html.OwaspHtmlTemplateOutput;
import gg.jte.output.StringOutput;
import heddle.benchmarks.jvm.engines.JteEngines;
import heddle.benchmarks.jvm.engines.ThymeleafEngines;
import heddle.benchmarks.jvm.jte.FiveEntityHtmlOutput;
import heddle.benchmarks.jvm.model.Models;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.function.UnaryOperator;

/**
 * Gate runner CLI (spec D11; verbs and message shapes per README &sect;Diagnostics and
 * harness-and-jmh.md &sect;Gate runner implementation contract):
 *
 * <pre>
 *   GateCli probe
 *   GateCli gate [--engine jte|thymeleaf] [--track controlled|idiomatic] [--workload &lt;id&gt;]
 *   GateCli calibrate
 * </pre>
 *
 * Exit codes: 0 = all pass; 1 = probe/gate/calibrate failure; 2 = corpus not found or
 * manifest sha256 mismatch.
 */
public final class GateCli {

    private static final String RAW_PAYLOAD = "<script>alert(";
    private static final String ESCAPED_PAYLOAD = "&lt;script&gt;alert(";

    /**
     * Controlled cells recorded as excluded via the feasibility-doc evidence procedure
     * ("engine/workload"). None exist at WI1 time; WI2 populates this if the Thymeleaf
     * ladder hits a beyond-whitespace divergence (thymeleaf-exclusion-evidence.md).
     */
    private static final Set<String> EXCLUDED_CONTROLLED_CELLS = Set.of();

    private GateCli() {
    }

    public static void main(String[] args) {
        if (args.length == 0) {
            System.err.println("usage: GateCli probe | gate [--engine jte|thymeleaf]"
                    + " [--track controlled|idiomatic] [--workload <id>] | calibrate");
            System.exit(1);
        }
        try {
            int exit = switch (args[0]) {
                case "probe" -> probe();
                case "gate" -> gate(args);
                case "calibrate" -> calibrate();
                default -> {
                    System.err.println("GateCli: unknown verb '" + args[0] + "'");
                    yield 1;
                }
            };
            if (exit != 0) {
                System.exit(exit);
            }
        } catch (Corpus.CorpusException e) {
            System.err.println(e.getMessage());
            System.exit(2);
        }
    }

    // ---- probe (D5) --------------------------------------------------------------------

    private record ProbePath(String name, Map<Character, String> spellings,
                             UnaryOperator<String> escape) {
    }

    private static int probe() {
        char[] five = {'&', '<', '>', '"', '\''};

        Map<Character, String> canonical = Map.of(
                '&', "&amp;", '<', "&lt;", '>', "&gt;", '"', "&quot;", '\'', "&#39;");
        Map<Character, String> jteStockText = Map.of(
                '&', "&amp;", '<', "&lt;", '>', "&gt;", '"', "\"", '\'', "'");
        Map<Character, String> jteStockAttr = Map.of(
                '&', "&amp;", '<', "&lt;", '>', ">", '"', "&#34;", '\'', "&#39;");

        List<ProbePath> paths = List.of(
                new ProbePath("jte-custom/text", canonical,
                        v -> jteCustom(v, "td", null)),
                new ProbePath("jte-custom/attribute", canonical,
                        v -> jteCustom(v, "td", "data-tag")),
                new ProbePath("jte-stock/text", jteStockText,
                        v -> jteStock(v, "td", null)),
                new ProbePath("jte-stock/attribute", jteStockAttr,
                        v -> jteStock(v, "td", "data-tag")),
                new ProbePath("thymeleaf/text", canonical,
                        ThymeleafEngines::escapeTextViaEngine),
                new ProbePath("thymeleaf/attribute", canonical,
                        ThymeleafEngines::escapeAttributeViaEngine));

        // The pinned probe payload set (D5).
        String[] payloads = {"&<>\"'", "こんにちは", "<script>alert('xss')</script>"};

        System.out.println("Escaper probe (D5) — spellings per escaping path:");
        System.out.println();
        System.out.printf("%-22s %-7s %-7s %-7s %-7s %-7s%n",
                "path", "&", "<", ">", "\"", "'");
        int failures = 0;
        for (ProbePath path : paths) {
            String[] got = new String[five.length];
            for (int i = 0; i < five.length; i++) {
                char c = five[i];
                got[i] = path.escape().apply(String.valueOf(c));
                String want = path.spellings().get(c);
                if (!got[i].equals(want)) {
                    System.out.println("[FAIL] " + path.name() + ": char '" + c
                            + "' rendered \"" + got[i] + "\", expected \"" + want + "\"");
                    failures++;
                }
            }
            System.out.printf("%-22s %-7s %-7s %-7s %-7s %-7s%n",
                    path.name(), got[0], got[1], got[2], got[3], got[4]);

            for (String payload : payloads) {
                String expected = mapPayload(payload, path.spellings());
                String actual = path.escape().apply(payload);
                if (!actual.equals(expected)) {
                    System.out.println("[FAIL] " + path.name() + ": payload \"" + payload
                            + "\" rendered \"" + actual + "\", expected \"" + expected + "\"");
                    failures++;
                }
            }
        }
        System.out.println();
        if (failures == 0) {
            System.out.println("[PASS] all 6 escaping paths match the D5 spelling table"
                    + " byte-for-byte (payload set: &<>\"' / こんにちは /"
                    + " <script>alert('xss')</script>)");
            return 0;
        }
        System.out.println(failures + " probe failure(s) — contract evidence; escalate per"
                + " Phase 1 machinery, never normalize away.");
        return 1;
    }

    private static String jteCustom(String value, String tag, String attribute) {
        FiveEntityHtmlOutput out = new FiveEntityHtmlOutput();
        out.setContext(tag, attribute);
        out.writeUserContent(value);
        return out.toString();
    }

    private static String jteStock(String value, String tag, String attribute) {
        StringOutput sink = new StringOutput();
        OwaspHtmlTemplateOutput out = new OwaspHtmlTemplateOutput(sink);
        out.setContext(tag, attribute);
        out.writeUserContent(value);
        return sink.toString();
    }

    private static String mapPayload(String payload, Map<Character, String> spellings) {
        StringBuilder sb = new StringBuilder(payload.length() + 16);
        for (int i = 0; i < payload.length(); i++) {
            char c = payload.charAt(i);
            String spelling = spellings.get(c);
            sb.append(spelling != null ? spelling : String.valueOf(c));
        }
        return sb.toString();
    }

    // ---- gate --------------------------------------------------------------------------

    private static int gate(String[] args) {
        String engineFilter = null;
        String trackFilter = null;
        String workloadFilter = null;
        for (int i = 1; i < args.length; i++) {
            switch (args[i]) {
                case "--engine" -> engineFilter = argValue(args, ++i, "--engine");
                case "--track" -> trackFilter = argValue(args, ++i, "--track");
                case "--workload" -> workloadFilter = argValue(args, ++i, "--workload");
                default -> {
                    System.err.println("GateCli gate: unknown option '" + args[i] + "'");
                    return 1;
                }
            }
        }

        Corpus corpus = Corpus.load();
        int pass = 0;
        int fail = 0;
        int excluded = 0;
        for (Corpus.Entry entry : corpus.entries()) {
            if (workloadFilter != null && !workloadFilter.equals(entry.workload)) {
                continue;
            }
            for (String engine : new String[] {"jte", "thymeleaf"}) {
                if (engineFilter != null && !engineFilter.equals(engine)) {
                    continue;
                }
                for (String track : new String[] {"controlled", "idiomatic"}) {
                    if (trackFilter != null && !trackFilter.equals(track)) {
                        continue;
                    }
                    String cell = engine + "/" + track + "/" + entry.workload;
                    if ("controlled".equals(track)
                            && EXCLUDED_CONTROLLED_CELLS.contains(engine + "/" + entry.workload)) {
                        System.out.println("[EXCLUDED — documented evidence] " + cell);
                        excluded++;
                        continue;
                    }
                    String failure = runCell(entry, engine, track);
                    if (failure == null) {
                        System.out.println("[PASS] " + cell);
                        pass++;
                    } else {
                        System.out.println("[FAIL] " + cell + ": " + failure);
                        fail++;
                    }
                }
            }
        }
        System.out.println();
        System.out.println("gate summary: " + pass + " passed, " + fail + " failed, "
                + excluded + " excluded");
        return fail == 0 ? 0 : 1;
    }

    private static String argValue(String[] args, int index, String option) {
        if (index >= args.length) {
            throw new IllegalArgumentException(option + " requires a value");
        }
        return args[index];
    }

    /** Runs one cell's gate; returns null on pass, a failure description otherwise. */
    private static String runCell(Corpus.Entry entry, String engine, String track) {
        String candidate;
        try {
            candidate = render(entry, engine, track);
        } catch (JteEngines.MissingTemplate | IllegalStateException e) {
            return "missing template/resources — " + e.getMessage();
        }
        return "controlled".equals(track)
                ? controlledGate(entry, engine, candidate)
                : idiomaticGate(entry, engine, candidate);
    }

    private static String render(Corpus.Entry entry, String engine, String track) {
        if ("thymeleaf".equals(engine)) {
            return ThymeleafEngines.render(track, entry.workload);
        }
        String template = track + "/" + entry.workload + ".jte";
        Object model = modelFor(entry.workload);
        if (!entry.isEncoded()) {
            return JteEngines.renderPlain(template, model);
        }
        return "controlled".equals(track)
                ? JteEngines.renderHtmlControlled(template, model)
                : JteEngines.renderHtmlIdiomatic(template, model);
    }

    private static Object modelFor(String workload) {
        return switch (workload) {
            case "composed-page" -> Models.composed();
            case "trivial-substitution" -> Models.SUBSTITUTION;
            case "large-loop" -> Models.LOOP_ROWS;
            case "mixed-page" -> Models.MIXED;
            case "conditional-heavy" -> Models.CONDITIONAL_ROWS;
            case "fragment-heavy" -> Models.FRAGMENT_ROWS;
            case "fortunes-encoded" -> Models.FORTUNE_ROWS;
            case "encoded-loop" -> Models.ENCODED_ITEMS;
            default -> throw new IllegalArgumentException("Unknown workload '" + workload + "'");
        };
    }

    /**
     * The controlled byte gate (contract &sect;Controlled-track gate): normalize N1-N5,
     * apply N3b to both sides, compare non-whitespace UTF-8 bytes; encoded suites also
     * assert the security floor on the un-normalized candidate. Returns null on pass.
     */
    static String controlledGate(Corpus.Entry entry, String engine, String candidate) {
        if (entry.isEncoded()) {
            int raw = Verifier.countOccurrences(candidate, RAW_PAYLOAD);
            if (raw != 0) {
                return "Security floor failed: " + entry.workload + " / " + engine
                        + ": raw \"" + RAW_PAYLOAD + "\" found " + raw + " times (expected 0)";
            }
            int expectedEscaped = Verifier.countOccurrences(entry.golden, ESCAPED_PAYLOAD);
            int actualEscaped = Verifier.countOccurrences(
                    Normalizer.normalize(candidate, true), ESCAPED_PAYLOAD);
            if (actualEscaped != expectedEscaped) {
                return "Security floor failed: " + entry.workload + " / " + engine
                        + ": escaped \"" + ESCAPED_PAYLOAD + "\" found " + actualEscaped
                        + " times (expected " + expectedEscaped + ")";
            }
        }
        String normalized = Normalizer.normalize(candidate, entry.isEncoded());
        String strippedCandidate = Normalizer.strip(normalized);
        String strippedGolden = Normalizer.strip(entry.golden);
        byte[] act = strippedCandidate.getBytes(StandardCharsets.UTF_8);
        byte[] exp = strippedGolden.getBytes(StandardCharsets.UTF_8);
        if (java.util.Arrays.equals(exp, act)) {
            return null;
        }
        int diff = 0;
        int max = Math.min(exp.length, act.length);
        while (diff < max && exp[diff] == act[diff]) {
            diff++;
        }
        return "Controlled gate failed: " + entry.workload + " / " + engine
                + ". first diff at index " + diff + " (of exp " + exp.length + "/act "
                + act.length + ").\n  expected: " + excerptAround(strippedGolden, exp, diff)
                + "\n  actual:   " + excerptAround(strippedCandidate, act, diff);
    }

    /** ±40-char excerpt in a 120-char \n-escaped window (ParityCheck.Describe shape). */
    private static String excerptAround(String stripped, byte[] utf8, int byteIndex) {
        // Map the byte index back to a char index (the stripped strings are what we show).
        int charIndex = new String(utf8, 0, Math.min(byteIndex, utf8.length),
                StandardCharsets.UTF_8).length();
        int from = Math.max(0, charIndex - 40);
        int to = Math.min(stripped.length(), from + 120);
        String window = stripped.substring(from, Math.max(from, Math.min(to, stripped.length())));
        return "..." + window.replace("\n", "\\n") + "...";
    }

    /** The idiomatic verifier gate. Returns null on pass. */
    static String idiomaticGate(Corpus.Entry entry, String engine, String candidate) {
        List<String> failures = Verifier.verify(entry.verifier, candidate);
        if (failures.isEmpty()) {
            return null;
        }
        return "Idiomatic gate failed: " + entry.workload + " / " + engine + " "
                + String.join("; ", failures);
    }

    // ---- calibrate ---------------------------------------------------------------------

    private record Corruption(String kind, String expectedCheckKind, String corrupted) {
    }

    private static int calibrate() {
        Corpus corpus = Corpus.load();
        int failures = 0;
        boolean dirtyWarned = false;
        for (Corpus.Entry entry : corpus.entries()) {
            if (!dirtyWarned && entry.generatingCommit.endsWith("+dirty")) {
                System.out.println("[WARN] manifest generatingCommit carries +dirty ("
                        + entry.generatingCommit + ") — a committed +dirty manifest fails"
                        + " review by inspection");
                dirtyWarned = true;
            }
            List<String> golden = Verifier.verify(entry.verifier, entry.golden);
            if (!golden.isEmpty()) {
                System.out.println("[FAIL] " + entry.workload
                        + " calibration: golden rejected: " + golden.get(0));
                failures++;
                continue;
            }
            List<Corruption> corruptions;
            try {
                corruptions = corruptionsFor(entry);
            } catch (IllegalStateException e) {
                System.out.println("[FAIL] " + entry.workload + " calibration: " + e.getMessage());
                failures++;
                continue;
            }
            boolean allRejected = true;
            for (Corruption corruption : corruptions) {
                List<String> got = Verifier.verify(entry.verifier, corruption.corrupted());
                boolean rejectedWithKind = got.stream().anyMatch(
                        f -> f.startsWith("verifier " + corruption.expectedCheckKind() + ":"));
                if (!rejectedWithKind) {
                    String detail = got.isEmpty()
                            ? "was NOT rejected"
                            : "was rejected, but not by the '" + corruption.expectedCheckKind()
                                    + "' check (got: " + got.get(0) + ")";
                    System.out.println("[FAIL] " + entry.workload + " calibration: corruption '"
                            + corruption.kind() + "' " + detail);
                    allRejected = false;
                }
            }
            if (allRejected) {
                System.out.println("[PASS] " + entry.workload + " calibration: golden accepted, "
                        + corruptions.size() + " corruption(s) rejected with the correct check kind");
            } else {
                failures++;
            }
        }
        System.out.println();
        if (failures == 0) {
            System.out.println("calibrate: all 8 workloads calibrated"
                    + " (raw: 2 corruptions each, encoded: 3)");
            return 0;
        }
        System.out.println("calibrate: " + failures + " workload(s) failed calibration");
        return 1;
    }

    /**
     * The contract's synthesized corruptions (golden-corpus.md &sect;Verification), pinned
     * per workload exactly as Phase 1's {@code IdiomaticChecks} pins them: removed
     * row/segment, reordered sections, and (encoded only) unescaped payload -
     * encoded-loop's pinned escaped-&gt;raw pair is {@code &lt;angle&gt;} -&gt;
     * {@code <angle>} (the workload carries no script payload).
     */
    private static List<Corruption> corruptionsFor(Corpus.Entry entry) {
        Map<String, String[]> pins = calibrationPins(entry);
        List<Corruption> corruptions = new ArrayList<>(3);

        String[] removed = pins.get("removed");
        corruptions.add(new Corruption("removed-row", removed[1],
                deleteFirst(entry.golden, removed[0])));

        String[] swap = pins.get("swap");
        corruptions.add(new Corruption("reordered-section", "marker",
                swapFirst(entry.golden, swap[0], swap[1])));

        String[] unescape = pins.get("unescape");
        if (unescape != null) {
            corruptions.add(new Corruption("unescaped-payload", "forbidden",
                    replaceFirst(entry.golden, unescape[0], unescape[1])));
        }
        return corruptions;
    }

    private static Map<String, String[]> calibrationPins(Corpus.Entry entry) {
        Map<String, String[]> pins = new LinkedHashMap<>();
        switch (entry.workload) {
            case "composed-page" -> {
                // Delete the SectionSocial marker fragment; swap the leading SectionMeta and
                // SectionSocial marker fragments (both from the exported verifier markers).
                List<String> markers = entry.verifier.markers;
                pins.put("removed", new String[] {markers.get(1), "marker"});
                pins.put("swap", new String[] {markers.get(0), markers.get(1)});
            }
            case "trivial-substitution" -> {
                pins.put("removed", new String[] {"HB-2001", "value"});
                pins.put("swap", new String[] {"class=\"sku\"", "class=\"rating\""});
            }
            case "large-loop" -> {
                pins.put("removed", new String[] {"<tr><td>row-0</td><td>0</td></tr>", "value"});
                pins.put("swap", new String[] {"<tr><td>row-0</td><td>0</td></tr>",
                        "<tr><td>row-2500</td><td>2500</td></tr>"});
            }
            case "mixed-page" -> {
                pins.put("removed", new String[] {"<article class=\"card\">", "value"});
                pins.put("swap", new String[] {"<header>", "class=\"hero\""});
            }
            case "conditional-heavy" -> {
                pins.put("removed", new String[] {"unit-000", "value"});
                pins.put("swap", new String[] {"unit-000", "unit-100"});
            }
            case "fragment-heavy" -> {
                pins.put("removed", new String[] {"tile-00", "value"});
                pins.put("swap", new String[] {"tile-00", "tile-24"});
            }
            case "fortunes-encoded" -> {
                Models.FortuneRow row0 = Models.FORTUNE_ROWS.get(0);
                String firstRow = "<tr><td>" + row0.getId() + "</td><td>"
                        + FiveEntityHtmlOutput.escape(row0.getMessage()) + "</td></tr>";
                pins.put("removed", new String[] {firstRow, "value"});
                pins.put("swap", new String[] {"<tr><th>id</th><th>message</th></tr>",
                        "フレームワークのベンチマーク"});
                pins.put("unescape", new String[] {ESCAPED_PAYLOAD, RAW_PAYLOAD});
            }
            case "encoded-loop" -> {
                Models.EncodedLoopRow row0 = Models.ENCODED_ITEMS.get(0);
                String tag0 = FiveEntityHtmlOutput.escape(row0.getTag());
                String firstRow = "<tr><td data-tag=\"" + tag0 + "\">"
                        + FiveEntityHtmlOutput.escape(row0.getName()) + "</td><td>"
                        + FiveEntityHtmlOutput.escape(row0.getComment()) + "</td></tr>";
                pins.put("removed", new String[] {firstRow, "value"});
                pins.put("swap", new String[] {tag0, "item &lt;2500&gt;"});
                // Phase 1 pin: no script payload in this workload; the escaped->raw pair is
                // the comment's angle text (forbidden: <angle>).
                pins.put("unescape", new String[] {"&lt;angle&gt;", "<angle>"});
            }
            default -> throw new IllegalStateException(
                    "no calibration pins for workload '" + entry.workload + "'");
        }
        return pins;
    }

    private static String deleteFirst(String text, String segment) {
        int at = text.indexOf(segment);
        if (at < 0) {
            throw new IllegalStateException("calibration pin not found in golden: \""
                    + segment.substring(0, Math.min(48, segment.length())) + "\"");
        }
        return text.substring(0, at) + text.substring(at + segment.length());
    }

    private static String replaceFirst(String text, String from, String to) {
        int at = text.indexOf(from);
        if (at < 0) {
            throw new IllegalStateException("calibration pin not found in golden: \""
                    + from + "\"");
        }
        return text.substring(0, at) + to + text.substring(at + from.length());
    }

    /** Swaps the first occurrence of {@code a} with the first occurrence of {@code b} after it. */
    private static String swapFirst(String text, String a, String b) {
        int posA = text.indexOf(a);
        if (posA < 0) {
            throw new IllegalStateException("calibration pin not found in golden: \""
                    + a.substring(0, Math.min(48, a.length())) + "\"");
        }
        int posB = text.indexOf(b, posA + a.length());
        if (posB < 0) {
            throw new IllegalStateException("calibration pin not found after its pair: \""
                    + b.substring(0, Math.min(48, b.length())) + "\"");
        }
        return text.substring(0, posA) + b + text.substring(posA + a.length(), posB)
                + a + text.substring(posB + b.length());
    }
}
