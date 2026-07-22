package heddle.benchmarks.jvm.gate;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

/**
 * Loads the Phase 1 golden corpus (manifest + goldens + {@code .verify.json} files) and
 * verifies every entry's bytes against the manifest's SHA-256 at load, failing fast on
 * mismatch or absence (surfaced by {@link GateCli} as exit 2).
 *
 * Path resolution (spec D8 / harness-and-jmh.md): default
 * {@code ../../src/Heddle.Performance/GoldenCorpus/} relative to the harness working
 * directory ({@code benchmarks/jvm/}), overridable with {@code -Dheddle.corpus=<path>}.
 */
public final class Corpus {

    /** Thrown for missing-corpus / manifest-hash failures; GateCli maps it to exit 2. */
    public static final class CorpusException extends RuntimeException {
        public CorpusException(String message) {
            super(message);
        }

        public CorpusException(String message, Throwable cause) {
            super(message, cause);
        }
    }

    public static final class Entry {
        public final String workload;
        public final String suite;          // "raw" | "encoded"
        public final String file;
        public final long byteLength;
        public final String sha256;         // lowercase hex, no "sha256:" prefix
        public final String generatingCommit;
        public final String golden;         // N1-strict-decoded stored oracle text
        public final Verifier.Definition verifier;

        Entry(String workload, String suite, String file, long byteLength, String sha256,
              String generatingCommit, String golden, Verifier.Definition verifier) {
            this.workload = workload;
            this.suite = suite;
            this.file = file;
            this.byteLength = byteLength;
            this.sha256 = sha256;
            this.generatingCommit = generatingCommit;
            this.golden = golden;
            this.verifier = verifier;
        }

        public boolean isEncoded() {
            return "encoded".equals(suite);
        }
    }

    private final Path root;
    private final Map<String, Entry> entries;   // insertion-ordered by workload number

    private Corpus(Path root, Map<String, Entry> entries) {
        this.root = root;
        this.entries = entries;
    }

    public Path root() {
        return root;
    }

    /** Entries in manifest (workload-number) order. */
    public List<Entry> entries() {
        return List.copyOf(entries.values());
    }

    public Entry entry(String workload) {
        Entry e = entries.get(workload);
        if (e == null) {
            throw new CorpusException("Unknown workload id '" + workload + "'");
        }
        return e;
    }

    /** Resolves the corpus directory ({@code -Dheddle.corpus} override, D8 default). */
    public static Path resolveRoot() {
        String override = System.getProperty("heddle.corpus");
        if (override != null && !override.isBlank()) {
            Path p = Path.of(override);
            if (!Files.isDirectory(p)) {
                throw new CorpusException(
                        "Corpus not found at " + p + " (set -Dheddle.corpus=...)");
            }
            return p;
        }
        Path byConvention = Path.of("..", "..", "src", "Heddle.Performance", "GoldenCorpus");
        if (Files.isDirectory(byConvention)) {
            return byConvention;
        }
        // Convenience fallback when invoked from the repo root instead of benchmarks/jvm/.
        Path fromRepoRoot = Path.of("src", "Heddle.Performance", "GoldenCorpus");
        if (Files.isDirectory(fromRepoRoot)) {
            return fromRepoRoot;
        }
        throw new CorpusException("Corpus not found at " + byConvention.toAbsolutePath().normalize()
                + " (set -Dheddle.corpus=...)");
    }

    /** Loads and SHA-256-verifies the whole corpus. */
    public static Corpus load() {
        Path root = resolveRoot();
        Path manifestPath = root.resolve("manifest.json");
        if (!Files.isRegularFile(manifestPath)) {
            throw new CorpusException("Corpus not found at " + root.toAbsolutePath().normalize()
                    + " (set -Dheddle.corpus=...)");
        }
        ObjectMapper mapper = new ObjectMapper();
        JsonNode manifest;
        try {
            manifest = mapper.readTree(Files.readAllBytes(manifestPath));
        } catch (IOException e) {
            throw new CorpusException("Failed reading corpus manifest " + manifestPath, e);
        }
        Map<String, Entry> entries = new LinkedHashMap<>();
        for (JsonNode node : manifest.path("entries")) {
            String workload = node.path("workload").asText();
            String suite = node.path("suite").asText();
            String file = node.path("file").asText();
            long byteLength = node.path("byteLength").asLong();
            String hash = node.path("hash").asText();
            String generatingCommit = node.path("generatingCommit").asText();
            if (!hash.startsWith("sha256:")) {
                throw new CorpusException("Corpus entry '" + workload
                        + "': unsupported manifest hash format \"" + hash + "\"");
            }
            String expectedSha = hash.substring("sha256:".length()).toLowerCase(Locale.ROOT);

            Path goldenPath = root.resolve(file);
            byte[] bytes;
            try {
                bytes = Files.readAllBytes(goldenPath);
            } catch (IOException e) {
                throw new CorpusException("Corpus entry '" + workload + "': cannot read "
                        + goldenPath + " (set -Dheddle.corpus=...)", e);
            }
            if (bytes.length != byteLength) {
                throw new CorpusException("Corpus entry '" + workload
                        + "' bytes do not match manifest sha256 (byteLength " + bytes.length
                        + " != manifest " + byteLength + ")");
            }
            String actualSha = sha256Hex(bytes);
            if (!actualSha.equals(expectedSha)) {
                throw new CorpusException(
                        "Corpus entry '" + workload + "' bytes do not match manifest sha256");
            }

            String golden = Normalizer.decodeUtf8Strict(bytes);       // N1, strict

            Path verifyPath = root.resolve(workload + ".verify.json");
            Verifier.Definition verifier;
            try {
                verifier = Verifier.parse(mapper.readTree(Files.readAllBytes(verifyPath)));
            } catch (IOException e) {
                throw new CorpusException("Corpus entry '" + workload + "': cannot read "
                        + verifyPath, e);
            }

            entries.put(workload, new Entry(workload, suite, file, byteLength, expectedSha,
                    generatingCommit, golden, verifier));
        }
        if (entries.size() != 8) {
            throw new CorpusException("Corpus manifest at " + manifestPath
                    + " lists " + entries.size() + " entries; expected 8");
        }
        return new Corpus(root, entries);
    }

    static String sha256Hex(byte[] bytes) {
        MessageDigest digest;
        try {
            digest = MessageDigest.getInstance("SHA-256");
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException("SHA-256 unavailable", e);
        }
        byte[] hash = digest.digest(bytes);
        StringBuilder sb = new StringBuilder(hash.length * 2);
        for (byte b : hash) {
            sb.append(Character.forDigit((b >> 4) & 0xF, 16));
            sb.append(Character.forDigit(b & 0xF, 16));
        }
        return sb.toString();
    }
}
