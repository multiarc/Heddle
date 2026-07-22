package heddle.benchmarks.jvm.gate;

import java.util.function.Supplier;

/**
 * Benchmark-time gate assertions (spec D9/D11; harness-and-jmh.md &sect;JMH benchmark shape).
 * Called from every {@code *Bench} class's {@code @Setup(Level.Trial)} so each JMH fork
 * re-asserts its cells' gates in the same process that produces its numbers (contract gate
 * rule 2). A failed gate throws {@link IllegalStateException} with the D11 message shape;
 * JMH aborts the fork and it produces no numbers.
 *
 * The gate logic itself lives once, in {@link GateCli} (DRY: the same code serves the CLI
 * and every fork); this class only adds corpus caching and the throw-on-failure adapter.
 */
public final class Gates {

    private static volatile Corpus corpus;

    private Gates() {
    }

    private static Corpus corpus() {
        Corpus c = corpus;
        if (c == null) {
            synchronized (Gates.class) {
                c = corpus;
                if (c == null) {
                    corpus = c = Corpus.load();
                }
            }
        }
        return c;
    }

    /** Controlled byte gate (+ encoded security floor) for one cell; throws on failure. */
    public static void assertControlled(String workload, String engine,
                                        Supplier<String> render) {
        String failure = GateCli.controlledGate(corpus().entry(workload), engine, render.get());
        if (failure != null) {
            throw new IllegalStateException(failure);
        }
    }

    /** Idiomatic verifier gate for one cell; throws on failure. */
    public static void assertIdiomatic(String workload, String engine,
                                       Supplier<String> render) {
        String failure = GateCli.idiomaticGate(corpus().entry(workload), engine, render.get());
        if (failure != null) {
            throw new IllegalStateException(failure);
        }
    }
}
