using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Heddle.Data;
using Heddle.Generator.IntegrationTests.Fixtures;
using Heddle.Precompiled;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 WI10 (raison-d'être): rendering a precompiled template is a lookup, not a parse-and-compile. Hooking
    /// <see cref="AppDomain.AssemblyLoad"/> around the render, no ANTLR (<c>Heddle.Language</c>/<c>Antlr4</c>) or
    /// Roslyn (<c>Microsoft.CodeAnalysis</c>) assembly load is triggered — the precompiled path never enters the
    /// dynamic front end.
    /// </summary>
    [Collection("PrecompiledRegistry")]
    public class StartupProbeTests
    {
        private const string CartType = "Heddle.Generator.IntegrationTests.Fixtures.Cart";

        [Fact]
        public void PrecompiledRenderLoadsNoParserOrRoslyn()
        {
            var key = "views/probe-startup.heddle";
            var content = "@model(){{" + CartType + "}}@\\\n" +
                          "Cart @(Name): @(Count) items, featured @if(IsFeatured){{ yes }}@else(){{ no }}\n";
            var gen = DifferentialHarness.Generate(new[] { (key, content) });
            Assert.NotNull(gen.Assembly);
            PrecompiledTemplates.Register(gen.Assembly);

            var resolver = new TemplateResolver(@"C:\nonexistent\root.marker", false);
            var template = resolver.GetTemplate(key, "", out _, null, TemplatePathType.None);
            Assert.NotNull(template);

            var loaded = new List<string>();
            AssemblyLoadEventHandler handler = (_, e) => loaded.Add(e.LoadedAssembly.GetName().Name ?? "");
            AppDomain.CurrentDomain.AssemblyLoad += handler;
            try
            {
                // Warm and repeated renders — the precompiled adapter must never touch the parser/compiler.
                for (int i = 0; i < 5; i++)
                    template.Generate(new Cart { Name = "Basket", Count = i, IsFeatured = i % 2 == 0 });
            }
            finally
            {
                AppDomain.CurrentDomain.AssemblyLoad -= handler;
            }

            var offenders = loaded.Where(n =>
                n.IndexOf("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Antlr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("Heddle.Language", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Assert.True(offenders.Count == 0,
                "Precompiled render triggered parser/Roslyn load: " + string.Join(", ", offenders));
        }
    }
}
