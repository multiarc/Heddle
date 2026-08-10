using System;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Heddle.Data;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <para>The self-extending copy-constructor completeness guard. Reflection walks every public settable
    /// instance property of <see cref="TemplateOptions"/>, sets a synthesized non-default value on a fresh
    /// instance, copies it through <c>new TemplateOptions(source)</c>, and asserts the copy round-trips the value.
    /// Any later phase adding an option property inherits the guard for free; an unknown property type fails the
    /// test with an explicit "add a synthesizer" message — that failure is the omission guard.</para>
    /// <para>This pins the historically-missed <c>ProvideLanguageFeatures</c> copy bug and the
    /// <c>OutputProfile</c> addition.</para>
    /// </summary>
    public class TemplateOptionsCompletenessTests
    {
        private static object Synthesize(PropertyInfo property, object current)
        {
            var type = property.PropertyType;
            if (type == typeof(bool))
                return true;
            if (type == typeof(string))
                return "probe-" + property.Name;
            if (type == typeof(int))
                return (int)current + 41;
            if (type.IsEnum)
            {
                foreach (var value in Enum.GetValues(type))
                {
                    if (!value.Equals(current))
                        return value;
                }
                throw new InvalidOperationException($"Enum {type} has no value distinct from the current one.");
            }
            if (type == typeof(FunctionRegistry))
                return new FunctionRegistry();
            if (type == typeof(TextEncoder))
                return HtmlEncoder.Create(UnicodeRanges.All);   // A distinct, non-default encoder reference
            if (type == typeof(RenderBudget))
                return new RenderBudget { MaxRenderOps = 123 };   // A distinct, non-default budget reference
            if (type == typeof(object))
                return new object();

            throw new InvalidOperationException(
                $"No synthesizer for property '{property.Name}' of type '{type}'. Add a synthesizer to TemplateOptionsCompletenessTests.");
        }

        /// <summary>
        /// Every public settable property survives the copy constructor. Computed get-only properties
        /// (<c>FullPath</c>) and the ctor-set get-only <c>TemplateName</c> are covered by dedicated facts below.
        /// </summary>
        [Fact]
        public void CopyConstructorPreservesEveryPublicSettableProperty()
        {
            var settable = typeof(TemplateOptions)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetSetMethod(false) != null)
                .ToList();

            Assert.NotEmpty(settable);

            foreach (var property in settable)
            {
                // Fresh instance: derived properties (AllowCSharp→ExpressionMode) must not interfere.
                var source = new TemplateOptions();
                var current = property.GetValue(source);
                var probe = Synthesize(property, current);
                property.SetValue(source, probe);

                var copy = new TemplateOptions(source);
                var copied = property.GetValue(copy);

                if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                {
                    Assert.True(Equals(probe, copied),
                        $"Property '{property.Name}' was not copied: expected '{probe}', got '{copied}'. " +
                        "Add it to the TemplateOptions copy constructor.");
                }
                else
                {
                    Assert.True(ReferenceEquals(probe, copied),
                        $"Reference property '{property.Name}' was not copied (shallow copy expected). " +
                        "Add it to the TemplateOptions copy constructor.");
                }
            }
        }

        [Fact]
        public void CopyConstructorPreservesProvideLanguageFeatures()
        {
            var source = new TemplateOptions { ProvideLanguageFeatures = true };
            var copy = new TemplateOptions(source);
            Assert.True(copy.ProvideLanguageFeatures);
        }

        [Fact]
        public void CopyConstructorPreservesOutputProfile()
        {
            var source = new TemplateOptions { OutputProfile = OutputProfile.Html };
            var copy = new TemplateOptions(source);
            Assert.Equal(OutputProfile.Html, copy.OutputProfile);
        }

        [Fact]
        public void CopyConstructorPreservesTemplateNameWhenNoOverride()
        {
            var source = new TemplateOptions("original-name");
            var copy = new TemplateOptions(source);
            Assert.Equal("original-name", copy.TemplateName);
        }
    }
}
