using System;
using System.Collections.Generic;

namespace Heddle.Tests
{
    /// <summary>
    /// The prop-default conversion vectors, authored once and asserted by both tiers:
    /// <c>Heddle.Tests.DefaultConvertibleReflectionTests</c> drives the runtime's
    /// <c>PropConversion.CanConvertTypes</c> and <c>Heddle.Generator.Tests.DefaultConvertibleLockstepTests</c>
    /// drives the emitter's symbol-side <c>DefaultConvertible</c>. Plain data, no Heddle dependency — the same
    /// linked-vectors shape <c>LineIndexVectors</c> established.
    /// </summary>
    public static class PropDefaultConversionVectors
    {
        public static IEnumerable<object[]> Rows()
        {
            yield return new object[] { typeof(int), typeof(int), true };
            yield return new object[] { typeof(string), typeof(string), true };
            yield return new object[] { typeof(int), typeof(object), true };
            yield return new object[] { typeof(decimal), typeof(object), true };
            yield return new object[] { typeof(int), typeof(long), true };
            yield return new object[] { typeof(int), typeof(double), true };
            yield return new object[] { typeof(char), typeof(int), true };
            yield return new object[] { typeof(float), typeof(double), true };
            yield return new object[] { typeof(long), typeof(int), false };
            yield return new object[] { typeof(double), typeof(float), false };
            yield return new object[] { typeof(int), typeof(int?), true };
            yield return new object[] { typeof(int), typeof(long?), true };
            yield return new object[] { typeof(char), typeof(double?), true };
            yield return new object[] { typeof(int?), typeof(int?), true };      // identity, via the same branch
            yield return new object[] { typeof(int?), typeof(long?), true };
            yield return new object[] { typeof(char?), typeof(int?), true };
            yield return new object[] { typeof(long?), typeof(int?), false };    // narrowing, still refused
            yield return new object[] { typeof(int?), typeof(string), false };
            yield return new object[] { typeof(string), typeof(object), true };
            yield return new object[] { typeof(object), typeof(string), false };
            yield return new object[] { typeof(string), typeof(int), false };
            yield return new object[] { typeof(bool), typeof(int), false };
            yield return new object[] { typeof(int), typeof(bool), false };
        }

    }
}
