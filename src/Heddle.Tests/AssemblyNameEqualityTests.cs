using System.Reflection;
using Heddle.Native;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>
    /// The comparer keying the engine's assembly cache. Two of its properties held nothing in place and reverted
    /// green, so they are asserted here.
    /// </summary>
    public class AssemblyNameEqualityTests
    {
        /// <summary>
        /// Equality ignores the case of the name, so the hash has to as well. Hashing case-sensitively puts two
        /// names that compare equal in different buckets, and the cache then accepts the same assembly twice —
        /// which makes every type in it ambiguous to resolution.
        /// </summary>
        [Fact]
        public void NamesDifferingOnlyInCaseAreOneEntry()
        {
            var lower = new AssemblyName("contoso.models");
            var upper = new AssemblyName("Contoso.Models");

            Assert.True(AssemblyNameEqualityComparer.Instance.Equals(lower, upper));
            Assert.Equal(AssemblyNameEqualityComparer.Instance.GetHashCode(lower),
                AssemblyNameEqualityComparer.Instance.GetHashCode(upper));
        }

        /// <summary>
        /// A public key token is eight bytes or empty, but nothing in the framework promises that of a name a host
        /// hands over. Reading eight bytes from a shorter one throws out of <c>GetHashCode</c> — from inside a
        /// dictionary lookup, where no caller has reason to have a catch.
        /// </summary>
        [Theory]
        [InlineData(new byte[0])]
        [InlineData(new byte[] { 1 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7 })]
        [InlineData(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
        public void AShortPublicKeyTokenDoesNotThrowOutOfTheHash(byte[] token)
        {
            var name = new AssemblyName("contoso.models");
            name.SetPublicKeyToken(token);

            AssemblyNameEqualityComparer.Instance.GetHashCode(name);
        }
    }
}
