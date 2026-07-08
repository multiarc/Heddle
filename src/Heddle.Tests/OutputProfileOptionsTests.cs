using Heddle.Data;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Enum defaults, options identity (phase 2 D6), and CompileContext profile plumbing (D4):
    /// initialization from options, child-context snapshotting of the effective (possibly directive-flipped)
    /// profile, and effective-vs-options precedence.
    /// </summary>
    public class OutputProfileOptionsTests
    {
        [Fact]
        public void EnumValuesAreStable()
        {
            Assert.Equal(0, (int)OutputProfile.Text);
            Assert.Equal(1, (int)OutputProfile.Html);
        }

        [Fact]
        public void DefaultProfileIsHtmlOnBothConstructors()
        {
            Assert.Equal(OutputProfile.Html, new TemplateOptions().OutputProfile);
            Assert.Equal(OutputProfile.Html, new TemplateOptions("named").OutputProfile);
        }

        [Fact]
        public void OptionsDifferingOnlyByProfileAreNotEqual()
        {
            var text = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var html = new TemplateOptions { OutputProfile = OutputProfile.Html };
            Assert.False(text.Equals(html));
            Assert.False(text == html);
        }

        [Fact]
        public void OptionsWithEqualProfileAreEqualWithEqualHashes()
        {
            var a = new TemplateOptions { OutputProfile = OutputProfile.Html };
            var b = new TemplateOptions { OutputProfile = OutputProfile.Html };
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void OptionsDifferingOnlyByProfileHaveDifferentHashes()
        {
            var text = new TemplateOptions { OutputProfile = OutputProfile.Text };
            var html = new TemplateOptions { OutputProfile = OutputProfile.Html };
            Assert.NotEqual(text.GetHashCode(), html.GetHashCode());
        }

        [Fact]
        public void CompileContextInitializesFromOptions()
        {
            Assert.Equal(OutputProfile.Text,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text }).OutputProfile);
            Assert.Equal(OutputProfile.Html,
                new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Html }).OutputProfile);
        }

        [Fact]
        public void ParameterlessCompileContextIsHtml()
        {
            Assert.Equal(OutputProfile.Html, new CompileContext().OutputProfile);
        }

        [Fact]
        public void ChildContextSnapshotsEffectiveProfileNotOptions()
        {
            // Parent options say Text; a directive flips the effective profile to Html mid-compile.
            var parent = new CompileContext(new TemplateOptions { OutputProfile = OutputProfile.Text });
            parent.OutputProfile = OutputProfile.Html;

            var child = new CompileContext(parent, (ExType)typeof(object));

            // Child snapshots the effective (flipped) value, not the untouched options value.
            Assert.Equal(OutputProfile.Html, child.OutputProfile);
            Assert.Equal(OutputProfile.Text, parent.Options.OutputProfile);
            Assert.Equal(OutputProfile.Text, child.Options.OutputProfile);
        }
    }
}
