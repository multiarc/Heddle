using Xunit;

namespace Heddle.Generator.Tests
{
    /// <summary>The refusal-construction gate: every degrade reason the emitter records is a categorized
    /// <c>Refusal</c> built through its one constructor. Pins by source text, the way
    /// <see cref="EmitterSharedRuleAdoptionTests"/> does, so a bare string reason cannot come back under any
    /// spelling the type system would accept.</summary>
    public class RefusalConstructionGateTests
    {
        private static string EmitterSource()
        {
            var dir = System.IO.Path.GetDirectoryName(typeof(RefusalConstructionGateTests).Assembly.Location);
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++, dir = System.IO.Path.GetDirectoryName(dir))
            {
                var candidate = System.IO.Path.Combine(dir, "Heddle.Generator", "Emit", "TemplateEmitter.cs");
                if (System.IO.File.Exists(candidate))
                    return System.IO.File.ReadAllText(candidate);
            }

            Assert.Fail("TemplateEmitter.cs was not found by walking up from " +
                        typeof(RefusalConstructionGateTests).Assembly.Location +
                        ". This gate reads the emitter's source and must not be skipped.");
            return null;
        }

        /// <summary>A refusal channel typed <c>string</c> is a bare reason waiting to happen. The emitter's
        /// channels are all <c>Refusal</c>-typed and every recorded refusal names its category at the construction
        /// site — a new <c>out string reason</c> parameter or a literal string assigned to a reason channel is a
        /// regression this fails on sight.</summary>
        [Fact]
        public void EveryEmitterRefusalIsConstructedWithACategory()
        {
            var source = EmitterSource();
            Assert.DoesNotContain("out string reason", source);
            Assert.DoesNotContain("reason = \"", source);
            Assert.DoesNotContain("Reason = \"", source);
            Assert.Contains("new Refusal(RefusalCategory.", source);
        }
    }
}
