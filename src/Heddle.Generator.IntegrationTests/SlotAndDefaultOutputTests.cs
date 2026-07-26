using System;
using System.Collections.Generic;
using Heddle.Generator.IntegrationTests.Fixtures;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// Slot-mode definitions (<c>&lt;name(out:: T)&gt;</c> + <c>@out(value)</c>) and definition default output
    /// (<c>-&gt; chain</c> rendered at document end via <c>ParseContext.DefaultChains</c>). Both bind the same
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

        [Fact]
        public void DefaultOutput_ModelLess()
        {
            // Definition renders once at document end, not on each call.
            var t = "@%\n<card> -> ()\n{{CARD}}\n%@\n";
            AssertParity("views/default-once.heddle", t, typeof(object), null);
        }

        [Fact]
        public void DefaultOutput_DoubleRender()
        {
            // By-name call and default chain both render; default chain also renders at end.
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
            // Definition iterates Options, projecting caller content via @out(this) for each item.
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
            // @out(this) projects the definition's own model through the slot.
            var t = "@model(){{" + ArticleType + "}}@\\\n" +
                    "@%\n<frame(out:: " + ArticleType + ")>{{[frame:@out(this)]}} :: " + ArticleType + "\n%@\n" +
                    "@frame(this){{<b>@(Title)</b>}}\n";
            AssertParity("views/slot-frame.heddle", t, typeof(Article), model);
        }
    }
}
