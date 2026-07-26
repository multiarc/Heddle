using System;
using System.Linq;
using Heddle.Language.Binding;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// <see cref="ExportBookkeeping{TPayload}"/> shipped with <b>no test at all</b>. It is the shared implementation
    /// of the runtime's <c>AddOrReplace</c> merge semantics — the rule that decides how many manifest rows a function
    /// name gets and what each row's overload count is — and the gauntlet compares those counts <em>exactly</em>, in
    /// both directions. An untested rule in that position is critical.
    /// <para>Lives in <c>Heddle.Tests</c> deliberately: the file compiles into the <c>Heddle</c> assembly, so a
    /// mutation of it reddens a <b>runtime</b> leg rather than only the generator's.</para>
    /// </summary>
    public class ExportBookkeepingTests
    {
        private const string First = "Ns.First, Asm";
        private const string Second = "Ns.Second, Asm";

        private static ExportOverload<string> Overload(string container, string payload, params string[] parameters) =>
            new ExportOverload<string>
            {
                ContainerAqn = container,
                ParameterTypeKeys = parameters,
                Payload = payload
            };

        [Fact]
        public void AFreshNameRecordsOneOverloadAndOneRow()
        {
            var book = new ExportBookkeeping<string>();

            Assert.True(book.AddOrReplace("slug", Overload(First, "Slug(string)", "System.String")));

            Assert.Equal(new[] { "slug" }, book.Names);
            Assert.Single(book.Overloads("slug"));
            Assert.Equal(new[] { First }, book.Containers("slug"));
            Assert.Equal(1, book.OverloadCount("slug", First));
        }

        [Fact]
        public void ADistinctSignatureInTheSameContainerAppendsAnOverload()
        {
            var book = new ExportBookkeeping<string>();
            book.AddOrReplace("slug", Overload(First, "Slug(string)", "System.String"));

            Assert.True(book.AddOrReplace("slug", Overload(First, "Slug(int)", "System.Int32")));

            Assert.Equal(2, book.Overloads("slug").Count);
            // One container, one row, count 2 — the shape the gauntlet compares against the live registry.
            Assert.Equal(new[] { First }, book.Containers("slug"));
            Assert.Equal(2, book.OverloadCount("slug", First));
        }

        [Fact]
        public void AnIdenticalSignatureReplacesRatherThanAppending()
        {
            var book = new ExportBookkeeping<string>();
            book.AddOrReplace("slug", Overload(First, "first", "System.String"));

            // The runtime's AddOrReplace rule: same name + identical parameter types replaces in place.
            Assert.False(book.AddOrReplace("slug", Overload(Second, "second", "System.String")));

            var only = Assert.Single(book.Overloads("slug"));
            Assert.Equal("second", only.Payload);
            Assert.Equal(Second, only.ContainerAqn);
            // …and the displaced container leaves no row behind, so no manifest row names a target the live
            // registry does not hold.
            Assert.Equal(new[] { Second }, book.Containers("slug"));
            Assert.Equal(0, book.OverloadCount("slug", First));
            Assert.Equal(1, book.OverloadCount("slug", Second));
        }

        [Fact]
        public void ASecondContainerMergesItsOverloadsUnderTheSameName()
        {
            var book = new ExportBookkeeping<string>();
            book.AddOrReplace("slug", Overload(First, "First.Slug(string)", "System.String"));
            book.AddOrReplace("slug", Overload(Second, "Second.Slug(int)", "System.Int32"));

            // Merge rule: one row PER container, each with its own count. First-container-wins recorded a single
            // row and made every merged name a permanent FunctionBindingMismatch.
            Assert.Equal(2, book.Overloads("slug").Count);
            Assert.Equal(new[] { First, Second }, book.Containers("slug"));
            Assert.Equal(1, book.OverloadCount("slug", First));
            Assert.Equal(1, book.OverloadCount("slug", Second));
        }

        [Fact]
        public void ParameterlessAndDistinctAritiesAreDistinctSignatures()
        {
            var book = new ExportBookkeeping<string>();
            book.AddOrReplace("now", Overload(First, "Now()"));
            Assert.True(book.AddOrReplace("now", Overload(First, "Now(string)", "System.String")));

            Assert.Equal(2, book.OverloadCount("now", First));
        }

        [Fact]
        public void SignatureIdentityIsOrderedAndOrdinal()
        {
            var book = new ExportBookkeeping<string>();
            book.AddOrReplace("pair", Overload(First, "(string,int)", "System.String", "System.Int32"));

            // Same types, different order — a different signature, so it appends.
            Assert.True(book.AddOrReplace("pair", Overload(First, "(int,string)", "System.Int32", "System.String")));
            // Case differs — ordinal comparison, so also a different signature.
            Assert.True(book.AddOrReplace("pair", Overload(First, "(STRING,int)", "System.string", "System.Int32")));

            Assert.Equal(3, book.OverloadCount("pair", First));
        }

        [Fact]
        public void NamesArePreservedInFirstRegistrationOrder()
        {
            var book = new ExportBookkeeping<string>();
            book.AddOrReplace("zeta", Overload(First, "z"));
            book.AddOrReplace("alpha", Overload(First, "a"));
            book.AddOrReplace("zeta", Overload(First, "z2", "System.String"));

            // Registration order, not sorted: the manifest's row order is derived from it, and a re-registration of
            // an existing name must not move it.
            Assert.Equal(new[] { "zeta", "alpha" }, book.Names);
        }

        [Fact]
        public void AnUnknownNameHasNoOverloadsRowsOrCounts()
        {
            var book = new ExportBookkeeping<string>();

            Assert.Empty(book.Overloads("missing"));
            Assert.Empty(book.Containers("missing"));
            Assert.Equal(0, book.OverloadCount("missing", First));
        }

        /// <summary>The signature-identity predicate the merge rule is built on, exercised directly — including the
        /// null and length guards the bookkeeping relies on.</summary>
        [Fact]
        public void SameSignatureGuardsNullAndLength()
        {
            Assert.True(ExportRules.SameSignature(new[] { "a", "b" }, new[] { "a", "b" }));
            Assert.False(ExportRules.SameSignature(new[] { "a", "b" }, new[] { "b", "a" }));
            Assert.False(ExportRules.SameSignature(new[] { "a" }, new[] { "a", "b" }));
            Assert.False(ExportRules.SameSignature(null, new[] { "a" }));
            Assert.False(ExportRules.SameSignature(new[] { "a" }, null));
            Assert.True(ExportRules.SameSignature(Array.Empty<string>(), Array.Empty<string>()));
        }
    }
}
