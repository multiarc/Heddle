[assembly: Heddle.Attributes.ExportFunctions(
    typeof(Heddle.Generator.IntegrationTests.Fixtures.DeprecatedTemplateFunctions))]

namespace Heddle.Generator.IntegrationTests.Fixtures
{
    /// <summary>
    /// Exported host functions carrying <c>[Obsolete]</c>, in both of its forms. Reflection ignores the attribute
    /// entirely, so the engine calls every one of them; the generated file spells the call into the
    /// <em>consumer's</em> assembly, where the error form is CS0619 and the build the template belongs to stops
    /// compiling. The warning form raises nothing there — the generated file opens with a blanket
    /// <c>#pragma warning disable</c> — so it must not cost the precompiled tier anything.
    /// </summary>
    public static class DeprecatedTemplateFunctions
    {
        [System.Obsolete("gone", true)]
        public static string RGoneStr(int a) => "gs" + a;

        [System.Obsolete("gone", true)]
        public static Article RGoneObj(int a) => new Article { Title = "go" + a };

        /// <summary>An export whose <b>return</b> type the consumer may not name. Declaring it needs an obsolete
        /// context of its own, and warning-level is the weakest one that gives it — which also makes this the row
        /// that tells the method's own attribute apart from its signature's.</summary>
        [System.Obsolete("an obsolete context is the only way to declare this")]
        public static ObsoleteErrorValue RGoneReturn(int a) => new ObsoleteErrorValue();

        /// <summary>The form that must keep precompiling: a deprecation note, not a refusal.</summary>
        [System.Obsolete("prefer rokstr")]
        public static string RWarnStr(int a) => "ws" + a;

        /// <summary>The near neighbour with no attribute at all, so a test can tell "this rule" from "this
        /// container".</summary>
        public static string ROkStr(int a) => "os" + a;
    }
}
