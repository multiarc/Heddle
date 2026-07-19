# ssr-aspnetcore

**Shows:** classic server-side rendering on an ASP.NET Core minimal API — a `TemplateResolver` composing pages
from `@partial` header/footer, iterating with `@list` and `@for` (phase 4 sugar), under the `Html` profile with
`TrimDirectiveLines` on, and the resolver's compiled-template cache. **Source of record:** roadmap current-engine
demo, upgraded per the 2.0 ergonomics work — `@for` + [whitespace trimming](../../docs/language-reference.md#whitespace-trimming-) (phase 9 D13 row 1).

## Run it

```bash
dotnet run --project samples/ssr-aspnetcore
```

Then open <http://localhost:5000/> (the article list) and <http://localhost:5000/articles/1> (one article). The
templates live in `templates/`: `index.heddle` and `article.heddle` each pull in `_header.heddle`/`_footer.heddle`
via `@partial`, and `@for(Stars)` renders a star rating.

The single `TemplateResolver` caches each compiled template keyed by path+profile+trim, so the second request for a
page reuses the compiled template — there is no public compile counter, so the observable contract is that the
**same page rendered twice is byte-identical** (asserted in capture mode).

## Capture mode (what CI runs)

```bash
dotnet run --project samples/ssr-aspnetcore -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/ssr-aspnetcore
```

Binds an ephemeral port, GETs `/` (twice — the cache assertion) and `/articles/1` over `HttpClient`, writes
`index.html` and `article-1.html`, and shuts down.

## What the golden pins

Both response bodies. They exercise resolver file resolution, `@partial` composition, `@list`/`@for` sugar, the
`Html` profile, and `TrimDirectiveLines` end-to-end through a real HTTP round-trip.
