using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Phase 7 — slot-mode definitions (<c>&lt;name(out:: T)&gt;</c> + <c>@out(value)</c>) and definition default
    /// output (<c>-&gt; chain</c> rendered at document end via <c>ParseContext.DefaultChains</c>). Both bind the same
    /// engine-internal carriers the runtime backend builds; differential-gated byte-for-byte.
    /// </summary>
    public class SlotAndDefaultOutputTests
    {
        private const string MenuType = "Heddle.Generator.IntegrationTests.Fixtures.Menu";
        private const string OptionType = "Heddle.Generator.IntegrationTests.Fixtures.MenuOption";
        private const string ArticleType = "Heddle.Generator.IntegrationTests.Fixtures.Article";

        private static void AssertParity(string key, string content, Type modelType, object model)
        {
            var (precompiled, dyn) = DifferentialHarness.Render(key, content, modelType, model);
            Assert.Equal(dyn, precompiled);
        }

        // ---- Default output ('-> chain' renders at document end) ----

        [Fact]
        public void DefaultOutput_ModelLess()
        {
            // ergo-double-render shape without the double call: the definition renders once at document end.
            var t = "@%\n<card> -> ()\n{{CARD}}\n%@\n";
            AssertParity("views/default-once.heddle", t, typeof(object), null);
        }

        [Fact]
        public void DefaultOutput_DoubleRender()
        {
            // The by-name call renders once and the default chain renders again at document end (HED4002 warning).
            var t = "@%\n<card> -> ()\n{{CARD}}\n%@\n@card()\n";
            AssertParity("views/default-double.heddle", t, typeof(object), null);
        }

        // ---- Slots ----

        public static IEnumerable<object[]> Menus()
        {
            yield return new object[]
            {
                new Menu { Options = new List<MenuOption>
                {
                    new MenuOption { Id = 1, Label = "Home" },
                    new MenuOption { Id = 2, Label = "About" },
                } }
            };
            yield return new object[] { new Menu { Options = new List<MenuOption>() } };
            yield return new object[] { new Menu { Options = null } };
        }

        [Theory]
        [MemberData(nameof(Menus))]
        public void Slot_PickerProjectsCallerContent(Menu model)
        {
            // slot-picker shape: the definition iterates Options and projects the caller content per option via
            // @out(this) — the caller body is typed by the declared slot type (MenuOption).
            var t = "@model(){{" + MenuType + "}}@\\\n" +
                    "@%\n<picker(out:: " + OptionType + ")>{{<ul>@list(Options){{<li>@out(this)</li>}}</ul>}} :: " + MenuType + "\n%@\n" +
                    "@picker(this){{<a href=\"/go?id=@(Id)\">@(Label)</a>}}\n";
            AssertParity("views/slot-picker.heddle", t, typeof(Menu), model);
        }

        public static IEnumerable<object[]> Articles()
        {
            yield return new object[] { new Article { Title = "Hello", Summary = "S" } };
            yield return new object[] { new Article { Title = null, Summary = null } };
        }

        [Theory]
        [MemberData(nameof(Articles))]
        public void Slot_SingleValueProjection(Article model)
        {
            // A slot definition that projects a single value: @out(this) passes the definition's own model through.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%\n<frame(out:: " + ArticleType + ")>{{[frame:@out(this)]}} :: " + ArticleType + "\n%@\n" +
                    "@frame(this){{<b>@(Title)</b>}}\n";
            AssertParity("views/slot-frame.heddle", t, typeof(Article), model);
        }
    }
}
