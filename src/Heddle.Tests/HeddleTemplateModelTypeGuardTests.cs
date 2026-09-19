using System;
using System.Reflection;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The model-type guard. A wrong-typed model is refused with "Type mismatch. Need X but got Y"
    /// in every configuration and whatever <see cref="TemplateOptions.ValidateModelType"/> says — without it the
    /// value reaches the compiled accessor's cast and escapes as a raw <see cref="InvalidCastException"/>, which is
    /// not the shape any other render fault has. The precompiled adapter is still skipped: it binds a strategy
    /// directly and has no compile-time model type to check against.
    /// </summary>
    public class HeddleTemplateModelTypeGuardTests
    {
        public class Flag { public bool A { get; set; } }

        /// <summary>
        /// Opt-in on: a wrong-typed model throws the DEBUG-shaped mismatch exception in every configuration,
        /// and a correctly-typed model still renders.
        /// </summary>
        [Fact]
        public void OptInValidateModelTypeThrowsOnMismatch()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateModelTypeGuardTests).GetTypeInfo().Assembly);
            using var template = new HeddleTemplate("STATIC",
                new CompileContext(new TemplateOptions { ValidateModelType = true }, typeof(Flag)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            var exception = Assert.Throws<TemplateProcessingException>(() => template.Generate(new object()));
            Assert.StartsWith("Type mismatch. Need ", exception.Message);
            Assert.Contains(typeof(Flag).FullName, exception.Message);
            Assert.Contains(typeof(object).FullName, exception.Message);

            Assert.Equal("STATIC", template.Generate(new Flag()));
        }

        /// <summary>
        /// The opt-in left off, which is its default: a wrong-typed model is refused all the same. This is what
        /// keeps the fault Heddle-shaped on the path that has no guard to fall back on.
        /// </summary>
        [Fact]
        public void MismatchIsValidatedWithTheOptInLeftOff()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateModelTypeGuardTests).GetTypeInfo().Assembly);
            using var template = new HeddleTemplate("STATIC", new CompileContext(typeof(Flag)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            var exception = Assert.Throws<TemplateProcessingException>(() => template.Generate(new object()));
            Assert.StartsWith("Type mismatch. Need ", exception.Message);

            Assert.Equal("STATIC", template.Generate(new Flag()));
        }



        /// <summary>
        /// A precompiled-adapter template binds a strategy directly, so there is no compile-time model type to
        /// check a model against. It is skipped rather than guessed at.
        /// </summary>
        [Fact]
        public void PrecompiledPathHasNoModelTypeToValidateAgainst()
        {
            using var template = new HeddleTemplate(new StaticStrategy("P"));   // the internal precompiled-adapter ctor (IVT)

            Assert.Equal("P", template.Generate(new object()));
        }

        private sealed class StaticStrategy : IProcessStrategy
        {
            private readonly string _text;

            public StaticStrategy(string text)
            {
                _text = text;
            }

            public string Execute(in Scope scope) => _text;

            public void Render(in Scope scope)
            {
                scope.Renderer.Render(_text);
            }
        }
    }
}
