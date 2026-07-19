using System;
using System.Reflection;
using Heddle.Data;
using Heddle.Exceptions;
using Heddle.Runtime;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Phase 4 WI3 — the opt-in Release model-type guard (<see cref="TemplateOptions.ValidateModelType"/>,
    /// P4-Q2): opted in, a wrong-typed model throws the same "Type mismatch. Need X but got Y"
    /// <see cref="TemplateProcessingException"/> the <c>DEBUG</c> guard has always thrown; off (the default),
    /// Release behavior is unchanged; <c>DEBUG</c> always validates regardless of the flag; the precompiled
    /// adapter is skipped in both configurations (the documented known limit).
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

#if !DEBUG
        /// <summary>
        /// Opt-in off (the default) in Release: wrong-typed data is not validated — today's Release behavior,
        /// byte-identical (the template renders its static body).
        /// </summary>
        [Fact]
        public void MismatchWithoutOptInIsNotValidatedInRelease()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateModelTypeGuardTests).GetTypeInfo().Assembly);
            using var template = new HeddleTemplate("STATIC", new CompileContext(typeof(Flag)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            Assert.Equal("STATIC", template.Generate(new object()));
        }
#endif

#if DEBUG
        /// <summary>
        /// The preserved historical behavior: in DEBUG the guard fires unconditionally, even with the
        /// opt-in left off.
        /// </summary>
        [Fact]
        public void DebugGuardFiresRegardlessOfOptIn()
        {
            HeddleTemplate.Configure(typeof(HeddleTemplateModelTypeGuardTests).GetTypeInfo().Assembly);
            using var template = new HeddleTemplate("STATIC",
                new CompileContext(new TemplateOptions { ValidateModelType = false }, typeof(Flag)));
            Assert.True(template.CompileResult.Success, template.CompileResult.ToString());

            var exception = Assert.Throws<TemplateProcessingException>(() => template.Generate(new object()));
            Assert.StartsWith("Type mismatch. Need ", exception.Message);
        }
#endif

        /// <summary>
        /// The documented known limit: a precompiled-adapter template (no compile-time model type to check
        /// against) is never validated, even with the opt-in requested — mirroring the DEBUG guard's skip.
        /// </summary>
        [Fact]
        public void PrecompiledPathIsNotValidatedEvenWhenOptedIn()
        {
            using var template = new HeddleTemplate(new StaticStrategy("P"));   // the internal precompiled-adapter ctor (IVT)
            // Force the opt-in on the resolved field: even then, the precompiled path skips the guard.
            var field = typeof(HeddleTemplate).GetField("_validateModelType",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            field.SetValue(template, true);

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
