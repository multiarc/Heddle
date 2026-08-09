using Heddle.TestInventory;
using Xunit;

namespace Heddle.Generator.IntegrationTests
{
    /// <summary>
    /// The membership gate for this suite: the classes that declare tests here are exactly the classes checked in
    /// as <c>src/Heddle.Generator.IntegrationTests/test-classes.txt</c>, compared as a set. It is what CI has instead of a
    /// hand-maintained test-count floor — a floor moved a digit and named nothing; this names the class that
    /// appeared or vanished. It does <b>not</b> notice a single <c>[Fact]</c> removed from a class that still
    /// declares others; see <see cref="TestClassInventory"/> for the full statement of what it covers.
    /// </summary>
    public class TestClassInventoryTests
    {
        [Fact]
        public void ThisSuiteDeclaresExactlyTheTestClassesCheckedIn()
        {
            TestClassInventory.AssertMatchesCheckedInInventory(
                typeof(TestClassInventoryTests).Assembly, "src/Heddle.Generator.IntegrationTests/test-classes.txt");
        }
    }
}
