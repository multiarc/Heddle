# MVC Integration

The `Heddle.Mvc` package lets you use Heddle templates as ASP.NET Core MVC views. It provides
an [`IViewEngine`](https://learn.microsoft.com/aspnet/core/mvc/views/view-components)
implementation that resolves `.heddle` files by controller and view name and renders them with
the model supplied by MVC.

Source: [src/Heddle.Mvc](../src/Heddle.Mvc).

---

## Components

| Type | Source | Role |
| --- | --- | --- |
| `HeddleViewEngine` | [HeddleViewEngine.cs](../src/Heddle.Mvc/HeddleViewEngine.cs) | `IViewEngine`; finds views/partials by name and controller. |
| `HeddleView` | [HeddleView.cs](../src/Heddle.Mvc/HeddleView.cs) | `IView` wrapper around a compiled `HeddleTemplate`; renders to the response writer. |
| `PartialMvcExtension` | [PartialMvcExtension.cs](../src/Heddle.Mvc/Extensions/PartialMvcExtension.cs) | MVC‑aware `@partial()` resolution. |
| `ImportMvcExtension` | [UseMvcExtension.cs](../src/Heddle.Mvc/Extensions/UseMvcExtension.cs) | MVC‑aware `@import` (named `import`). |

`HeddleViewEngine` holds a `TemplateResolver` rooted at the web root's parent directory and uses
it to locate templates. `HeddleView.RenderAsync` simply writes `template.Generate(model)` to the
response:

```csharp
public async Task RenderAsync(ViewContext context)
{
    await context.Writer.WriteAsync(_template.Generate(context.ViewData.Model));
}
```

---

## Registering the view engine

Register `HeddleViewEngine` with MVC's view‑engine collection and make sure extensions are
configured at startup. In a typical ASP.NET Core app:

```csharp
using Heddle;            // HeddleTemplate.Configure
using Heddle.Mvc;        // HeddleViewEngine

var builder = WebApplication.CreateBuilder(args);

// Register the engine's extensions (built‑ins + the MVC package's overrides).
HeddleTemplate.Configure(typeof(HeddleViewEngine).Assembly);

builder.Services.AddControllersWithViews()
    .AddViewOptions(options =>
    {
        // HeddleViewEngine needs IWebHostEnvironment (resolved from DI).
        var env = builder.Services.BuildServiceProvider().GetRequiredService<IWebHostEnvironment>();
        options.ViewEngines.Insert(0, new HeddleViewEngine(env));
    });

var app = builder.Build();
app.MapControllers();
app.Run();
```

> `HeddleViewEngine`'s constructor takes the host environment
> (`IWebHostEnvironment`; `IHostingEnvironment` on `netstandard2.0`) and roots its resolver at
> `Path.GetDirectoryName(WebRootPath)`. Adapt the registration to however you obtain that
> environment in your composition root. Inserting it at index 0 makes it take precedence over
> the default Razor engine for the view names it can resolve.

---

## View resolution

When MVC asks the engine for a view, `HeddleViewEngine`:

1. Reads the **controller** name from route data (`RouteData.Values["controller"]`).
2. Asks its `TemplateResolver` for the template by **view name** + controller, as a `View` or
   `PartialView` ([`TemplatePathType`](../src/Heddle/Runtime)).
3. Returns `ViewEngineResult.Found(viewName, (HeddleView)result)` when a matching compiled
   template exists, otherwise `ViewEngineResult.NotFound(viewName, searchedLocations)`.

`FindView` handles main pages and delegates partials to `FindPartialView`; `GetView`
(path‑based lookup) returns *not found* — resolution is name/controller based, not raw‑path
based.

Controllers return views as usual:

```csharp
public class HomeController : Controller
{
    public IActionResult Index() => View(model);   // resolves Home/Index.heddle
}
```

The `.heddle` convention is the project's standard template extension (see the test fixtures in
[src/Heddle.Tests/TestTemplate](../src/Heddle.Tests/TestTemplate)); ensure your
`TemplateResolver`/options use that postfix.

---

## MVC‑specific extensions

The MVC assembly exports two extensions via `[assembly: ExportExtensions(...)]`, so they are
registered whenever you `Configure` with the `Heddle.Mvc` assembly:

- **`PartialMvcExtension`** — derives from
  [`PartialExtension`](../src/Heddle/Extensions/PartialExtension.cs) and is intended to
  resolve `@partial()` targets through the MVC `TemplateResolver` (by controller/view), rather
  than as plain files relative to `RootPath`.
- **`ImportMvcExtension`** — registered under the name `import`, replacing the core
  [`ImportExtension`](../src/Heddle/Extensions/ImportExtension.cs) so imports resolve
  through MVC view locations.

> Note: in the current source both MVC extensions contain commented‑out resolver wiring (the
> service‑provider lookup is stubbed). Treat them as the integration seam for MVC‑aware
> partial/import resolution; verify and complete the wiring for your hosting setup before
> relying on MVC‑relative `@partial()`/`@import`. Because `[ExtensionName]` registration is
> last‑wins, whichever assembly you pass last to `Configure` determines the active `import`/
> `partial` behavior.

---

## Targets

`Heddle.Mvc` targets `net6.0;net8.0` (with `netstandard2.0`/`2.1` build outputs present)
and references the ASP.NET Core shared framework. See [Building & Testing](building.md) for
framework details.
