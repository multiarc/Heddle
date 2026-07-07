# streaming-ssr

**Shows:** streaming SSR — rendering directly into `Response.BodyWriter` (an `IBufferWriter<byte>`) then
`FlushAsync`, with no intermediate output string; plus a `TextWriter` sink for contrast, and byte/string parity.
**Source of record:** [phase 8](../../docs/spec/phase-8-streaming-async/README.md) (phase 9 D13 row 10).

## Run it

```bash
dotnet run --project samples/streaming-ssr
```

- `GET /` renders with `template.Generate(model, Response.BodyWriter)` and `await Response.BodyWriter.FlushAsync()`
  — the bytes go straight to the response pipe.
- `GET /text-writer` renders through a `StreamWriter` over `Response.Body` for contrast.

Both use the same compiled template under the `Html` profile, so encoding (`&lt;`, `&amp;`, …) is exercised on the
byte path.

## Capture mode (what CI runs)

```bash
dotnet run --project samples/streaming-ssr -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/streaming-ssr
```

Self-requests both endpoints and writes `streamed.html` and `textwriter.html`. It also asserts the phase 8
**encoding-parity** rule at integration level: the `IBufferWriter<byte>` output decoded as UTF-8 equals the
`string Generate` output *and* the `TextWriter` output for the same template + data (a mismatch fails the run
before any golden is written).

## What the golden pins

Both response bodies. The parity assertion is the cross-sink contract; the goldens pin the shared rendered output.
