using System;
using Heddle.Runtime.Expressions;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 1 WI3 (D4) — the reflection side of the prop-default conversion lockstep. Same rows as
    /// <c>Heddle.Generator.Tests.DefaultConvertibleLockstepTests</c>, from the linked
    /// <see cref="PropDefaultConversionVectors"/>: the runtime is normative, the emitter's symbol-side table must
    /// answer identically, and the vectors are what make "identically" checkable rather than asserted by eye.
    /// </summary>
    public class DefaultConvertibleReflectionTests
    {
        [Theory]
        [MemberData(nameof(PropDefaultConversionVectors.Rows), MemberType = typeof(PropDefaultConversionVectors))]
        public void RuntimeRuleMatchesTheVectors(Type source, Type target, bool expected)
        {
            Assert.Equal(expected, PropConversion.CanConvertTypes(source, target, allowBoxToObject: true));
        }
    }
}
