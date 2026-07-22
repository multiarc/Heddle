package heddle.benchmarks.jvm.bench;

import heddle.benchmarks.jvm.engines.JteEngines;
import heddle.benchmarks.jvm.engines.ThymeleafEngines;
import heddle.benchmarks.jvm.gate.Gates;
import heddle.benchmarks.jvm.model.Models;
import org.openjdk.jmh.annotations.Benchmark;
import org.openjdk.jmh.annotations.BenchmarkMode;
import org.openjdk.jmh.annotations.Fork;
import org.openjdk.jmh.annotations.Level;
import org.openjdk.jmh.annotations.Measurement;
import org.openjdk.jmh.annotations.Mode;
import org.openjdk.jmh.annotations.OutputTimeUnit;
import org.openjdk.jmh.annotations.Scope;
import org.openjdk.jmh.annotations.Setup;
import org.openjdk.jmh.annotations.State;
import org.openjdk.jmh.annotations.Threads;
import org.openjdk.jmh.annotations.Warmup;

import java.util.concurrent.TimeUnit;

/**
 * composed-page (raw suite) - WI5 benchmark class. Annotations are the spec D9 pin: JMH
 * 1.37's defaults stated explicitly (JMHSample_13 state-your-settings discipline); no
 * jvmArgs, no CompilerControl, no per-class deviations. Engines and models are built once
 * per fork; {@code @Setup(Level.Trial)} re-asserts this workload's four cell gates in the
 * same process that produces the numbers (contract gate rule 2, spec D11) - a gate throw
 * aborts the fork with no numbers. Every {@code @Benchmark} method is exactly one render
 * into a fresh output buffer and returns the rendered String (JMH's implicit Blackhole,
 * JMHSample_08/09).
 */
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
@Fork(5)
@Warmup(iterations = 5, time = 10, timeUnit = TimeUnit.SECONDS)
@Measurement(iterations = 5, time = 10, timeUnit = TimeUnit.SECONDS)
@Threads(1)
@State(Scope.Benchmark)
public class ComposedPageBench {

    private static final String WORKLOAD = "composed-page";

    private Object model;

    @Setup(Level.Trial)
    public void setup() {
        model = Models.composed();
        Gates.assertControlled(WORKLOAD, "jte", this::renderJteControlled);
        Gates.assertControlled(WORKLOAD, "thymeleaf", this::renderThymeleafControlled);
        Gates.assertIdiomatic(WORKLOAD, "jte", this::renderJteIdiomatic);
        Gates.assertIdiomatic(WORKLOAD, "thymeleaf", this::renderThymeleafIdiomatic);
    }

    @Benchmark
    public String jteControlled() {
        return renderJteControlled();
    }

    @Benchmark
    public String jteIdiomatic() {
        return renderJteIdiomatic();
    }

    @Benchmark
    public String thymeleafControlled() {
        return renderThymeleafControlled();
    }

    @Benchmark
    public String thymeleafIdiomatic() {
        return renderThymeleafIdiomatic();
    }

    private String renderJteControlled() {
        return JteEngines.renderPlain("controlled/composed-page.jte", model);
    }

    private String renderJteIdiomatic() {
        return JteEngines.renderPlain("idiomatic/composed-page.jte", model);
    }

    private String renderThymeleafControlled() {
        return ThymeleafEngines.render("controlled", WORKLOAD);
    }

    private String renderThymeleafIdiomatic() {
        return ThymeleafEngines.render("idiomatic", WORKLOAD);
    }
}
