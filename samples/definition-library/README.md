# definition-library

**Shows:** composition imports — pulling a shared definition library in with `@<<`, overriding one definition
locally, and narrowing an abstract definition to a concrete type through `<name:name>` inheritance.
**Source of record:** roadmap current-engine demo (phase 9 D13 row 2).

## Run it

```bash
dotnet run --project samples/definition-library
```

`templates/library.heddle` defines three definitions (`greeting`, `footer`, an abstract `region`).
`templates/page.heddle` imports them with `@<<{{library.heddle}}`, then:

- **overrides** `greeting` with a page-local `<greeting:greeting>` (document-order override — later calls use it), and
- **narrows** the abstract `region` to `Article` with `<region:region> … :: Article`, so its body can read `@(Title)`.

The rendered page shows the *page's* greeting, the *library's* footer (imported unchanged), and the narrowed region.

> Definitions imported via `@<<` are used as declared — this sample keeps the library's imported bodies static and
> puts member expressions in the page-side override/narrow bodies. (Member expressions inside a body that is *both*
> imported and re-parsed at a non-zero offset are subject to a pre-existing positioning limitation; the override and
> narrowing bodies compiled in the page itself are unaffected.)

## Capture mode (what CI runs)

```bash
dotnet run --project samples/definition-library -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/definition-library
```

## What the golden pins

`page.html`. The `greeting` line proves the override won; the `footer` line proves the import; the `region` line
proves the abstract→`Article` narrowing (it reads a typed member the abstract base never had).
