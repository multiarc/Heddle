using System;
using System.Collections.Generic;
using System.Reflection;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// D11 parameterized slots: the picker projection (caller body rendered once per option, option as model),
    /// the slot diagnostics HED5012–HED5014/HED5018, the composition case, and the runtime guard.
    /// </summary>
    public class SlotProjectionTests
    {
        private const string Picker =
            "@% <picker(out:: PropMenuOption)>{{<ul>@list(Options){{<li>@out(this)</li>}}</ul>}} :: PropMenu %@\n";

        private static HeddleTemplate Compile(string document, ExType modelType, TemplateOptions options = null)
        {
            HeddleTemplate.Configure(typeof(SlotProjectionTests).GetTypeInfo().Assembly);
            return new HeddleTemplate(document, new CompileContext(options ?? new TemplateOptions(), modelType));
        }

        private static PropRoot MenuRoot() => new PropRoot
        {
            Menu = new PropMenu
            {
                Options = new List<PropMenuOption>
                {
                    new PropMenuOption { Id = 1, Label = "A" },
                    new PropMenuOption { Id = 2, Label = "B" }
                }
            }
        };

        [Fact]
        public void PickerRendersOncePerOption()
        {
            var t = Compile(Picker + "@picker(Menu){{<a>@(Id):@(Label)</a>}}", typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("<ul><li><a>1:A</a></li><li><a>2:B</a></li></ul>", t.Generate(MenuRoot()).Trim());
        }

        [Fact]
        public void CallerBodyTypedAgainstSlot()
        {
            // The caller body binds against MenuOption; a member not on MenuOption is a positioned HED0001.
            var t = Compile(Picker + "@picker(Menu){{@(NoSuchField)}}", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.PropertyNotFound);
        }

        [Fact] // HED5012 — slot-less definition body
        public void OutValueInSlotlessDefinition()
        {
            var t = Compile("@% <box>{{@out(this)}} :: PropArticle %@\n@box(Article)", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.SlotValueWithoutSlot && e.Error.Contains("slot parameter"));
        }

        [Fact] // HED5012 — outside any definition body
        public void OutValueOutsideDefinition()
        {
            var t = Compile("@out(Article)", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.SlotValueWithoutSlot &&
                     e.Error.Contains("inside a definition body"));
        }

        [Fact] // HED5012 — legacy @out(true) (P20's runtime half)
        public void LegacyOutLiteralIsError()
        {
            var t = Compile("@% <box>{{@out(true)}} :: PropArticle %@\n@box(Article)", typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SlotValueWithoutSlot);
        }

        [Fact] // HED5013 — empty @out() in a slot-declaring body
        public void EmptyOutInSlotDefinition()
        {
            var t = Compile("@% <picker(out:: PropMenuOption)>{{@out()}} :: PropMenu %@\n@picker(Menu){{x}}",
                typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SlotValueRequired);
        }

        [Fact] // HED5014 — value type not assignable to the slot type
        public void SlotValueTypeMismatch()
        {
            var t = Compile("@% <picker(out:: PropMenuOption)>{{@out(this)}} :: PropMenu %@\n@picker(Menu){{x}}",
                typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SlotValueTypeMismatch);
        }

        [Fact] // HED5014 — dynamic slot value form
        public void SlotValueDynamicForm()
        {
            var t = Compile("@% <dyn(out:: PropMenuOption)>{{@out(this)}} :: dynamic %@\n@dyn(this){{x}}",
                ExType.Dynamic);
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors,
                e => e.DiagnosticId == HeddleDiagnosticIds.SlotValueTypeMismatch && e.Error.Contains("dynamic"));
        }

        [Fact] // HED5018 — @out(expr) with a body
        public void SlotOutWithBody()
        {
            var t = Compile(
                "@% <picker(out:: PropMenuOption)>{{@list(Options){{@out(this){{X}}}}}} :: PropMenu %@\n@picker(Menu){{x}}",
                typeof(PropRoot));
            Assert.False(t.CompileResult.Success);
            Assert.Contains(t.CompileResult.Errors, e => e.DiagnosticId == HeddleDiagnosticIds.SlotValueWithBody);
        }

        [Fact] // runtime guard — a slot-mode @out chained after a producer throws
        public void ChainedCompositionGuardThrows()
        {
            const string doc =
                "@% <nested>{{n}} :: PropArticle" +
                " <sd(out:: PropArticle)>{{@nested():out(this)}} :: PropArticle %@\n@sd(Article){{X}}";
            var t = Compile(doc, typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Throws<TemplateProcessingException>(() =>
                t.Generate(new PropRoot { Article = new PropArticle() }));
        }

        [Fact] // composition — nested non-slot caller content projects the OUTER slot
        public void SlotCompositionProjectsOuterSlot()
        {
            const string doc =
                "@% <inner>{{[in:@out()]}} :: PropArticle" +
                " <outer(out:: PropArticle)>{{@inner(){{<B>@(Title)</B>}}}} :: PropArticle %@\n" +
                "@outer(Article){{<OUT:@(Title)>}}";
            var t = Compile(doc, typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[in:<B>T</B>]",
                t.Generate(new PropRoot { Article = new PropArticle { Title = "T" } }).Trim());
        }

        [Fact] // @out byte-identical in a non-slot definition (bodiless splice unchanged)
        public void NonSlotOutStillSplices()
        {
            var t = Compile("@% <wrap>{{[@out()]}} :: PropArticle %@\n@wrap(Article){{HELLO}}", typeof(PropRoot));
            Assert.True(t.CompileResult.Success, t.CompileResult.ToString());
            Assert.Equal("[HELLO]", t.Generate(new PropRoot { Article = new PropArticle() }).Trim());
        }
    }
}
