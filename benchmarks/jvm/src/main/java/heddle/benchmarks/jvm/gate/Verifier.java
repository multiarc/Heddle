package heddle.benchmarks.jvm.gate;

import com.fasterxml.jackson.databind.JsonNode;

import java.util.ArrayList;
import java.util.List;

/**
 * Java implementation of the Phase 1 idiomatic-track verifier
 * (parity-contract-v2.md &sect;Idiomatic-track gate), consuming the exported
 * {@code <workload>.verify.json} definitions. Matching semantics mirror the intra-.NET
 * {@code IdiomaticChecks.Verify}: the candidate is normalized N1-N4 (+N5 encoded), then
 * the N3b whitespace strip is applied to the output AND to every needle before matching;
 * {@code values} are exact non-overlapping counts, {@code markers} are strictly ordered,
 * {@code forbidden} must be absent from both raw and normalized output, {@code required}
 * is a minimum count. Failure messages start with {@code "verifier <kind>: "}.
 */
public final class Verifier {

    public record ValueCheck(String text, int count) {
    }

    public record RequiredCheck(String text, int minCount) {
    }

    public static final class Definition {
        public final String workload;
        public final String suite;
        public final List<ValueCheck> values;
        public final List<String> markers;
        public final List<String> forbidden;
        public final List<RequiredCheck> required;

        Definition(String workload, String suite, List<ValueCheck> values,
                   List<String> markers, List<String> forbidden, List<RequiredCheck> required) {
            this.workload = workload;
            this.suite = suite;
            this.values = values;
            this.markers = markers;
            this.forbidden = forbidden;
            this.required = required;
        }

        public boolean isEncoded() {
            return "encoded".equals(suite);
        }
    }

    private Verifier() {
    }

    /** Parses one {@code <workload>.verify.json} document (already Jackson-parsed). */
    public static Definition parse(JsonNode root) {
        List<ValueCheck> values = new ArrayList<>();
        for (JsonNode v : root.path("values")) {
            values.add(new ValueCheck(v.path("text").asText(), v.path("count").asInt()));
        }
        List<String> markers = new ArrayList<>();
        for (JsonNode m : root.path("markers")) {
            markers.add(m.asText());
        }
        List<String> forbidden = new ArrayList<>();
        for (JsonNode f : root.path("forbidden")) {
            forbidden.add(f.asText());
        }
        List<RequiredCheck> required = new ArrayList<>();
        for (JsonNode r : root.path("required")) {
            required.add(new RequiredCheck(r.path("text").asText(), r.path("minCount").asInt()));
        }
        return new Definition(root.path("workload").asText(), root.path("suite").asText(),
                values, markers, forbidden, required);
    }

    /**
     * Runs the verifier against a candidate's raw output. Returns an empty list when
     * accepted; otherwise one message per failed check.
     */
    public static List<String> verify(Definition def, String rawOutput) {
        List<String> failures = new ArrayList<>();
        String normalized = Normalizer.normalize(rawOutput, def.isEncoded());
        String stripped = Normalizer.strip(normalized);
        String strippedRaw = Normalizer.strip(rawOutput);

        for (ValueCheck v : def.values) {
            int found = countOccurrences(stripped, Normalizer.strip(v.text()));
            if (found != v.count()) {
                failures.add("verifier value: expected " + v.count() + " of \""
                        + excerpt(v.text()) + "\", found " + found);
            }
        }

        int pos = 0;
        for (String marker : def.markers) {
            String needle = Normalizer.strip(marker);
            int at = stripped.indexOf(needle, pos);
            if (at < 0) {
                failures.add("verifier marker: expected 1 of \"" + excerpt(marker)
                        + "\" in order after index " + pos + ", found 0");
                // Keep scanning subsequent markers from the current position so every
                // out-of-order/missing marker is reported.
                continue;
            }
            pos = at + needle.length();
        }

        for (String f : def.forbidden) {
            String needle = Normalizer.strip(f);
            int found = Math.max(countOccurrences(strippedRaw, needle),
                    countOccurrences(stripped, needle));
            if (found != 0) {
                failures.add("verifier forbidden: expected 0 of \"" + excerpt(f)
                        + "\", found " + found);
            }
        }

        for (RequiredCheck r : def.required) {
            int found = countOccurrences(stripped, Normalizer.strip(r.text()));
            if (found < r.minCount()) {
                failures.add("verifier required: expected " + r.minCount() + " of \""
                        + excerpt(r.text()) + "\", found " + found);
            }
        }

        return failures;
    }

    static int countOccurrences(String haystack, String needle) {
        if (needle.isEmpty()) {
            return 0;
        }
        int count = 0;
        int index = 0;
        while ((index = haystack.indexOf(needle, index)) >= 0) {
            count++;
            index += needle.length();
        }
        return count;
    }

    private static String excerpt(String s) {
        String cut = s.length() <= 48 ? s : s.substring(0, 48) + "…";
        return cut.replace("\n", "\\n");
    }
}
