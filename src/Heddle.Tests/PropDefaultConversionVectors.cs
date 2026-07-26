using System;
using System.Collections.Generic;

namespace Heddle.Tests
{
    /// <summary>
    /// Generator plan phase 1 WI3 (D4) — the prop-default conversion vectors, authored once and asserted by both
    /// tiers: <c>Heddle.Tests.DefaultConvertibleReflectionTests</c> drives the runtime's
    /// <c>PropConversion.CanConvertTypes</c> and <c>Heddle.Generator.Tests.DefaultConvertibleLockstepTests</c>
    /// drives the emitter's symbol-side <c>DefaultConvertible</c>. Plain data, no Heddle dependency — the same
    /// linked-vectors shape <c>LineIndexVectors</c> established (phase 6 D12.6).
    /// <para>This row set is the seed of phase 3's assignability conformance corpus and is handed to it verbatim.</para>
    /// </summary>
    public static class PropDefaultConversionVectors
    {
        public static IEnumerable<object[]> Rows()
        {
            // identity
            yield return new object[] { typeof(int), typeof(int), true };
            yield return new object[] { typeof(string), typeof(string), true };
            // boxing to object
            yield return new object[] { typeof(int), typeof(object), true };
            yield return new object[] { typeof(decimal), typeof(object), true };
            // implicit numeric widening
            yield return new object[] { typeof(int), typeof(long), true };
            yield return new object[] { typeof(int), typeof(double), true };
            yield return new object[] { typeof(char), typeof(int), true };
            yield return new object[] { typeof(float), typeof(double), true };
            // narrowing is not a conversion
            yield return new object[] { typeof(long), typeof(int), false };
            yield return new object[] { typeof(double), typeof(float), false };
            // identity-lift
            yield return new object[] { typeof(int), typeof(int?), true };
            // widen-then-lift
            yield return new object[] { typeof(int), typeof(long?), true };
            yield return new object[] { typeof(char), typeof(double?), true };
            // Nullable<S> -> Nullable<W>  (the phase 1 D4 rows)
            yield return new object[] { typeof(int?), typeof(int?), true };      // identity, via the same branch
            yield return new object[] { typeof(int?), typeof(long?), true };
            yield return new object[] { typeof(char?), typeof(int?), true };
            yield return new object[] { typeof(long?), typeof(int?), false };    // narrowing, still refused
            yield return new object[] { typeof(int?), typeof(string), false };
            // reference assignability (never value types)
            yield return new object[] { typeof(string), typeof(object), true };
            yield return new object[] { typeof(object), typeof(string), false };
            // outright refusals
            yield return new object[] { typeof(string), typeof(int), false };
            yield return new object[] { typeof(bool), typeof(int), false };
            yield return new object[] { typeof(int), typeof(bool), false };
        }

    }
}
