# MVC Integration

The `Templates.Mvc` package lets you use TTL templates as ASP.NET Core MVC views. It provides
an [`IViewEngine`](https://learn.microsoft.com/aspnet/core/mvc/views/view-components)
implementation that resolves `.thtml` files by controller and view name and renders them with
the model supplied by MVC.

Source: [src/Templates.Mvc](../src/Templates.Mvc).

---

## Components

| Type | Source | Role |
| --- | --- | --- |
| `TtlViewEngine` | [TtlViewEngine.cs](../src/Templates.Mvc/TtlViewEngine.cs) | `IViewEngine`; finds views/partials by name and controller. |
| `TtlView` | [TtlView.cs](../src/Templates.Mvc/TtlView.cs) | `IView` wrapper around a compiled `TtlTemplate`; renders to the response writer. |
| `PartialMvcExtension` | [PartialMvcExtension.cs](../src/Templates.Mvc/Extensions/PartialMvcExtension.cs) | MVC‑aware `@partial()` resolution. |
| `ImportMvcExtension` | [UseMvcExtension.cs](../src/Templates.Mvc/Extensions/UseMvcExtension.cs) | MVC‑aware `@import` (named `import`). |

`TtlViewEngine` holds a `TemplateResolver` rooted at the web root's parent directory and uses
it to locate templates. `TtlView.RenderAsync` simply writes `template.Generate(model)` to the
response:

```csharp
public async Task RenderAsync(ViewContext context)
{
    await context.Writer.WriteAsync(_template.Generate(context.ViewData.Model));
}
```

---

## Registering the view engine

Register `TtlViewEngine` with MVC's view‑engine collection and make sure extensions are
configured at startup. In a typical ASP.NET Core app:

```csharp
using Templates;            // TtlTemplate.Configure
using Templates.Mvc;        // TtlViewEngine

var builder = WebApplication.CreateBuilder(args);

// Register the engine's extensions (built‑ins + the MVC package's overrides).
TtlTemplate.Configure(typeof(TtlViewEngine).Assembly);

builder.Services.AddControllersWithViews()
    .AddViewOptions(options =>
    {
        // TtlViewEngine needs IWebHostEnvironment (resolved from DI).
        var env = builder.Services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>();
        options.ViewEngines.Insert(0, new TtlViewEngine(env));
    });

var app = builder.Build();
app.MapControllers();
app.Run();
```

> `TtlViewEngine`'s constructor takes the host environment
> (`IWebHostEnvironment`; `IHostingEnvironment` on `netstandard2.0`) and roots its resolver at
> `Path.GetDirectoryName(WebRootPath)`. Adapt the registration to however you obtain that
> environment in your composition root. Inserting it at index 0 makes it take precedence over
> the default Razor engine for the view names it can resolve.

---

## View resolution

When MVC asks the engine for a view, `TtlViewEngine`:

1. Reads the **controller** name from route data (`RouteData.Values["controller"]`).
2. Asks its `TemplateResolver` for the template by **view name** + controller, as a `View` or
   `PartialView` ([`TemplatePathType`](../src/Templates/Runtime)).
3. Returns `ViewEngineResult.Found(viewName, (TtlView)result)` when a matching compiled
   template exists, otherwise `ViewEngineResult.NotFound(viewName, searchedLocations)`.

`FindView` handles main pages and delegates partials to `FindPartialView`; `GetView`
(path‑based lookup) returns *not found* — resolution is name/controller based, not raw‑path
based.

Controllers return views as usual:

```csharp
public class HomeController : Controller
{
    public IActionResult Index() => View(model);   // resolves Home/Index.thtml
}
```

The `.thtml` convention is the project's standard template extension (see the test fixtures in
[src/Templates.Tests/TestTemplate](../src/Templates.Tests/TestTemplate)); ensure your
`TemplateResolver`/options use that postfix.

---

## MVC‑specific extensions

The MVC assembly exports two extensions via `[assembly: ExportExtensions(...)]`, so they are
registered whenever you `Configure` with the `Templates.Mvc` assembly:

- **`PartialMvcExtension`** — derives from
  [`PartialExtension`](../src/Templates/Extensions/PartialExtension.cs) and is intended to
  resolve `@partial()` targets through the MVC `TemplateResolver` (by controller/view), rather
  than as plain files relative to `RootPath`.
- **`ImportMvcExtension`** — registered under the name `import`, replacing the core
  [`ImportExtension`](../src/Templates/Extensions/ImportExtension.cs) so imports resolve
  through MVC view locations.

> Note: in the current source both MVC extensions contain commented‑out resolver wiring (the
> service‑provider lookup is stubbed). Treat them as the integration seam for MVC‑aware
> partial/import resolution; verify and complete the wiring for your hosting setup before
> relying on MVC‑relative `@partial()`/`@import`. Because `[ExtensionName]` registration is
> last‑wins, whichever assembly you pass last to `Configure` determines the active `import`/
> `partial` behavior.

---

## Targets

`Templates.Mvc` targets `net6.0;net8.0` (with `netstandard2.0`/`2.1` build outputs present)
and references the ASP.NET Core shared framework. See [Building & Testing](building.md) for
framework details.
