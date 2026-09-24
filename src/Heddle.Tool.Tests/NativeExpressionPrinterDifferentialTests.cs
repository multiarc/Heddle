using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Heddle.Tool.Compile;
using Heddle.Tool.Compile.Sites;
using Xunit;

[assembly: Heddle.Attributes.ExportFunctions(typeof(Heddle.Tool.Tests.LazyFunctions))]

namespace Heddle.Tool.Tests
{
    /// <summary>Every read is logged, so the two tiers can be compared on <i>what they evaluated and in
    /// which order</i>, not only on what they rendered.</summary>
    public static class LazyLog
    {
        public static readonly List<string> Entries = new List<string>();

        public static T Note<T>(string what, T value)
        {
            Entries.Add(what);
            return value;
        }
    }

    public static class LazyFunctions
    {
        public static int DiffTap(string tag, int value) => LazyLog.Note("tap:" + tag, value);

        public static int DiffLen(string value) => LazyLog.Note("len", value == null ? 0 : value.Length);

        public static List<int> DiffItems(int count)
        {
            LazyLog.Entries.Add("items:" + count);
            var items = new List<int>();
            for (int i = 0; i < count; i++)
                items.Add(i * 10);
            return items;
        }
    }

    public class LazyOwner
    {
        public string Name => LazyLog.Note("Owner.Name", "ada");
    }

    public class LazyBag
    {
        public string this[int index]
        {
            get
            {
                LazyLog.Entries.Add("Bag[" + index + "]");
                if (index != 0)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return "zero";
            }
        }
    }

    public class LazyModel
    {
        public bool HasOwner => LazyLog.Note("HasOwner", false);

        public bool Yes => LazyLog.Note("Yes", true);

        public string Label => LazyLog.Note("Label", "label");

        public LazyOwner Safe => LazyLog.Note("Safe", new LazyOwner());

        public LazyOwner Missing => LazyLog.Note<LazyOwner>("Missing", null);

        public LazyBag Bag => LazyLog.Note("Bag", new LazyBag());

        public List<List<int>> Matrix => LazyLog.Note("Matrix", new List<List<int>>());

        /// <summary>The booby trap: a guarded arm that reaches it was evaluated eagerly.</summary>
        public LazyOwner Owner
        {
            get
            {
                LazyLog.Entries.Add("Owner");
                throw new InvalidOperationException("a guarded operand was evaluated");
            }
        }
    }

    /// <summary>Declares an equality operator its derived type inherits. The engine's null tests and
    /// reference-equality arms compare references and never call it; C# overload resolution would.</summary>
    public class IdentityBase
    {
        public int Id { get; set; }

        public string Title => LazyLog.Note("Title", "titled");

        public static bool operator ==(IdentityBase left, IdentityBase right)
        {
            LazyLog.Entries.Add("operator==");
            return (left is null ? 0 : left.Id) == (right is null ? 0 : right.Id);
        }

        public static bool operator !=(IdentityBase left, IdentityBase right) => !(left == right);

        public override bool Equals(object obj) => ReferenceEquals(this, obj);

        public override int GetHashCode() => Id;
    }

    public class IdentityDerived : IdentityBase
    {
    }

    public class IdentityModel
    {
        private readonly IdentityDerived _unsaved = new IdentityDerived { Id = 0 };
        private readonly IdentityDerived _other = new IdentityDerived { Id = 0 };
        private readonly IdentityBase _plain = new IdentityBase { Id = 7 };

        public IdentityDerived Unsaved => LazyLog.Note("Unsaved", _unsaved);

        public IdentityDerived Other => LazyLog.Note("Other", _other);

        public IdentityBase Plain => LazyLog.Note("Plain", _plain);

        public IdentityBase SamePlain => LazyLog.Note("SamePlain", new IdentityBase { Id = 7 });

        /// <summary>Its own type declares the operator, so the engine's null test does call it.</summary>
        public IdentityBase ZeroPlain => LazyLog.Note("ZeroPlain", new IdentityBase { Id = 0 });

        public IdentityDerived Nothing => LazyLog.Note<IdentityDerived>("Nothing", null);

        public List<IdentityDerived> Items => LazyLog.Note("Items", new List<IdentityDerived> { _unsaved });
    }

    public enum DifferentialLevel
    {
        Below = -1,
        None = 0,
        Above = 1
    }

    public class DifferentialLeaf
    {
        public string Name { get; set; }
        public int[] Numbers { get; set; }
    }

    public class DifferentialModel
    {
        public int A { get; set; }
        public int B { get; set; }
        public int C { get; set; }
        public int X { get; set; }
        public long L { get; set; }
        public double D { get; set; }
        public int? N { get; set; }
        public bool F { get; set; }
        public bool G { get; set; }
        public bool H { get; set; }
        public string S { get; set; }
        public decimal Price { get; set; }
        public int Max { get; set; }
        public int Min { get; set; }
        public long BigL { get; set; }
        public char Ch { get; set; }
        public DifferentialLeaf Left { get; set; }
        public DifferentialLeaf Right { get; set; }
        public List<string> L1 { get; set; }
        public List<string> L2 { get; set; }
    }

    /// <summary>The printer's grouping proof: every nested operator shape renders the same bytes from the
    /// printed C# as from the dynamic tier. Pins the regression where a printed operand that merely began
    /// with <c>(</c> and ended with <c>)</c> was taken as already grouped, so <c>X / (A + B)</c> printed
    /// <c>(X) / (A) + (B)</c> and <c>!(A == B)</c> printed an uncompilable <c>!(A) == (B)</c>.</summary>
    [Collection("PrecompiledProcessStateSerial")]
    public class NativeExpressionPrinterDifferentialTests : IDisposable
    {
        private static readonly string[] IntegerOperators = { "+", "-", "*", "/", "%", "<<", ">>", "&", "|", "^" };

        private readonly string _dir;
        private readonly Heddle.Data.TemplateOptions _savedDefaultOptions;

        public NativeExpressionPrinterDifferentialTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "heddle-diff-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _savedDefaultOptions = Heddle.Precompiled.PrecompiledTemplates.DefaultOptions;
        }

        public void Dispose()
        {
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", false);
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
            }
        }

        [Fact]
        public void EveryIntegerOperatorPairGroupsIdenticallyOnBothTiers()
        {
            var expressions = new List<string>();
            foreach (var outer in IntegerOperators)
            {
                foreach (var inner in IntegerOperators)
                {
                    expressions.Add("A " + outer + " (B " + inner + " C)");
                    expressions.Add("(A " + outer + " B) " + inner + " C");
                    expressions.Add("A " + outer + " B " + inner + " C");
                }
            }

            AssertTiersAgree(expressions);
        }

        [Fact]
        public void UnaryTernaryReceiverAndConversionShapesGroupIdenticallyOnBothTiers()
        {
            AssertTiersAgree(new List<string>
            {
                "X / (A + B)",
                "X / A + B",
                "!(A == B)",
                "!(A != B) == (A < B)",
                "!(F && G) || H",
                "!F && (G || H)",
                "F == (A < B)",
                "(A < B) == (C > A)",
                "(A < B) != F",
                "-(A + B)",
                "-A + B",
                "-(-A)",
                "-(A * -B)",
                "+(A - B) * C",
                "~(A | B)",
                "~A | B",
                "~(-A)",
                "-(~A)",
                "(F ? A : B) + C",
                "F ? A : B + C",
                "F ? A : (G ? B : C)",
                "(F ? G : H) ? A : B",
                "(F ? A : B) > C ? \"x\" : \"y\"",
                "F ? (G ? \"a\" : \"b\") : (H ? \"c\" : \"d\")",
                "(Left ?? Right).Name",
                "(F ? Left : Right).Name",
                "(G ? Left : Right).Numbers[1]",
                "(F ? L1 : L2)[0]",
                "(G ? L1 : L2)[A - 28]",
                "(S ?? \"fallback\").Length",
                "(S + \"tail\").Length * 2",
                "(N ?? A) * 2",
                "N ?? A * 2",
                "(A + L) * D",
                "A + L * D",
                "(A + B) / D",
                "A / (L - B)",
                "X / (D + A)",
                "\"v\" + (A + B)",
                "\"v\" + A + B",
                "(A + B) + \"v\"",
                "Ch == 'q'",
                "Ch == '\\''",
                "'\\'' + \"s\"",
                "'\"' + \"s\"",
                "'\\\\' + \"s\"",
                "\"q\\\"uote\\\\\" + S",
                "A - -1",
                "A - (-1)",
                "A * -2 + -3",
                "-1 * (A - -B)",
                "(A - B) - C",
                "A - (B - C)",
                "(A / B) / C",
                "A / (B / C)",
                "(A << 1) << C",
                "A << (1 << C)",
                "(A > B) && (B > C) || (C > A)",
                "(A > B) && ((B > C) || (C > A))",
                "((A > B) || (B > C)) && (C > A)"
            });
        }

        /// <summary>The engine's tree evaluates a guarded operand only when its guard lets it, and everything
        /// else left to right. Pins the regression where the printer hoisted every null-safe hop's receiver
        /// into a statement ahead of the <c>return</c>, so the untaken arm of <c>?:</c>, <c>&amp;&amp;</c>,
        /// <c>||</c> and <c>??</c> ran anyway — a throwing getter or an out-of-range index threw on the
        /// precompiled tier only — and a hop's receiver was read before operands written to its left.</summary>
        [Fact]
        public void GuardedOperandsStayLazyAndSideEffectsKeepTheirOrderOnBothTiers()
        {
            var registry = new Heddle.Runtime.Expressions.FunctionRegistry();
            registry.RegisterFrom(typeof(LazyFunctions).Assembly);
            AssertTiersAgree(new List<string>
            {
                "HasOwner ? Owner.Name : \"-\"",
                "Yes ? \"+\" : Owner.Name",
                "Matrix.Count > 0 ? Matrix[0][1] : 0",
                "HasOwner && Owner.Name == \"x\"",
                "Yes || Owner.Name == \"x\"",
                "!HasOwner || Owner.Name == \"x\"",
                "Missing != null && Missing.Name == \"x\"",
                "Label ?? Owner.Name",
                "(Label ?? Owner.Name).Length",
                "HasOwner ? diffitems(0)[0] : -1",
                "Yes ? diffitems(2)[1] : diffitems(0)[5]",
                "HasOwner ? Bag[7] : Bag[0]",
                "HasOwner ? (Yes ? Owner.Name : Owner.Name) : (Yes ? \"t\" : Owner.Name)",
                "Yes ? (HasOwner && Owner.Name == \"x\" ? Owner.Name : Safe.Name) : Owner.Name",
                "(HasOwner ? Owner : Safe).Name",
                "(HasOwner && Owner.Name == \"x\") || (Yes && Safe.Name == \"ada\")",
                "Label ?? (HasOwner ? Owner.Name : Owner.Name)",
                "difftap(\"a\", 1) + Safe.Name.Length",
                "difftap(\"a\", 1) + Bag[difftap(\"i\", 0)].Length * difftap(\"z\", 3)",
                "Bag[difftap(\"i\", 0)] + Safe.Name + difftap(\"t\", 9)",
                "difftap(\"a\", 1) + (Yes ? difftap(\"b\", 2) : difftap(\"c\", 3)) * difftap(\"d\", 4)",
                "difftap(\"l\", Safe.Name.Length) > difftap(\"r\", Bag[0].Length) ? Safe.Name : Label",
                "diffitems(difftap(\"n\", 3))[difftap(\"k\", 2)] + Matrix.Count"
            }, typeof(LazyModel), new LazyModel(), registry);
        }

        /// <summary>The engine's arithmetic never checks for overflow, whatever the consumer's project says.
        /// Pins the regression where a site returned its bare expression: a constant sub-expression that
        /// overflows failed the consumer's build (CS0220), and a consumer compiled with overflow checking
        /// threw where the dynamic tier wraps. The printed source in this class is compiled with overflow
        /// checking on, so every site has to opt out by itself.</summary>
        [Fact]
        public void ArithmeticWrapsLikeTheEngineEvenWhenTheConsumerChecksForOverflow()
        {
            AssertTiersAgree(new List<string>
            {
                "A + (2147483647 + 1)",
                "X * (100000 * 100000)",
                "Max + 1",
                "Max + A",
                "Min - 1",
                "-Min",
                "Max * 2",
                "Max * Max",
                "(Max + 1) / 2",
                "BigL + 1",
                "BigL * 2",
                "BigL + Max",
                "Max + 1 > 0 ? \"positive\" : \"wrapped\"",
                "(F ? Max : Min) + (G ? 1 : 2)"
            });
        }

        /// <summary>A null test on a hop's receiver, and <c>==</c>/<c>!=</c> where the engine found no operator
        /// to bind, compare <b>references</b>. Pins the regression where they printed as a bare infix
        /// <c>==</c>, which C# binds to an equality operator the operand type merely inherits: a receiver
        /// whose operator calls it equal to null lost its members, and the operator ran where the engine
        /// never calls it. Where the engine did bind the operator, the printed form has to bind it too.</summary>
        [Fact]
        public void NullTestsAndUnboundEqualityCompareReferencesOnBothTiers()
        {
            AssertTiersAgree(new List<string>
            {
                "Unsaved.Title",
                "Unsaved.Title.Length",
                "Nothing.Title",
                "Plain.Title",
                "ZeroPlain.Title",
                "ZeroPlain.Title + Plain.Title.Length",
                "Items[0].Title",
                "(Nothing ?? Unsaved).Title",
                "Unsaved == null",
                "Unsaved != null",
                "null == Unsaved",
                "Nothing == null",
                "Unsaved == Other",
                "Unsaved != Other",
                "Unsaved == Unsaved",
                "Plain == SamePlain",
                "Plain != SamePlain",
                "Plain == Unsaved",
                "Unsaved == Plain",
                "Unsaved == null ? \"none\" : Unsaved.Title"
            }, typeof(IdentityModel), new IdentityModel(), null);
        }

        /// <summary>A standalone function call carries its body on the unnamed carrier the compiler puts
        /// behind it. Pins the regression where the form recorded that body against the carrier's private
        /// stand-in item, which the artifact never walks: the body was dropped from the artifact and the
        /// template failed to materialize ("no recorded body") the first time it was bound.</summary>
        [Fact]
        public void BodiedStandaloneFunctionCallsPrecompileAndRenderIdentically()
        {
            var registry = new Heddle.Runtime.Expressions.FunctionRegistry();
            registry.RegisterFrom(typeof(LazyFunctions).Assembly);
            AssertTiersAgree(new List<string>
            {
                "@difflen(Label){{[@(this)]}}",
                "@difflen(Label):difflen(Label){{(@(this))}}",
                "@difflen(Label){{<@(this)>@(this + 1)}}"
            }, typeof(LazyModel), new LazyModel(), registry, wholeLines: true);
        }

        [Theory]
        [InlineData('\'', "'\\''")]
        [InlineData('"', "'\"'")]
        [InlineData('\\', "'\\\\'")]
        [InlineData('\n', "'\\n'")]
        [InlineData('a', "'a'")]
        public void CharConstantsSpellAsCompilableCharLiterals(char value, string expected)
        {
            string literal;
            string why;
            Assert.True(NativeExpressionPrinter.TrySpellLiteral(value, typeof(char), out literal, out why), why);
            Assert.Equal(expected, literal);
            Assert.Equal(value, EvaluateConstant<char>(literal));
        }

        [Theory]
        [InlineData(DifferentialLevel.Below)]
        [InlineData(DifferentialLevel.None)]
        [InlineData(DifferentialLevel.Above)]
        public void EnumConstantsSpellAsCompilableCasts(DifferentialLevel value)
        {
            string literal;
            string why;
            Assert.True(NativeExpressionPrinter.TrySpellLiteral(value, typeof(DifferentialLevel), out literal,
                out why), why);
            Assert.Equal(value, EvaluateConstant<DifferentialLevel>(literal));
        }

        [Fact]
        public void BoundTreesWithNegativeAndEnumConstantsPrintCompilableEquivalentCode()
        {
            var model = Expression.Parameter(typeof(object), "model");
            var chained = Expression.Parameter(typeof(object), "chained");
            var root = Expression.Parameter(typeof(object), "root");
            var level = Expression.Constant(DifferentialLevel.Below);
            var trees = new Expression[]
            {
                Expression.Equal(level, Expression.Constant(DifferentialLevel.Below)),
                Expression.Convert(Expression.Constant(DifferentialLevel.Below), typeof(int)),
                Expression.Convert(Expression.Constant(-5), typeof(long)),
                Expression.Negate(Expression.Constant(-5)),
                Expression.Subtract(Expression.Constant(3), Expression.Constant(-5)),
                Expression.Divide(Expression.Constant(100),
                    Expression.Add(Expression.Constant(20), Expression.Constant(5))),
                Expression.Not(Expression.Equal(Expression.Constant(1), Expression.Constant(2))),
                Expression.Convert(Expression.Add(Expression.Constant(1), Expression.Constant(2)), typeof(double)),
                Expression.Add(Expression.Convert(Expression.Constant((short)-3), typeof(int)),
                    Expression.Constant(1)),
                Expression.Property(Expression.Coalesce(Expression.Constant(null, typeof(string)),
                    Expression.Constant("four")), "Length"),
                Expression.Property(Expression.Condition(Expression.Constant(true),
                    Expression.Constant("yes"), Expression.Constant("no")), "Length")
            };

            var source = new StringBuilder();
            source.Append("namespace DifferentialSites\n{\n    public static class Sites\n    {\n");
            for (int i = 0; i < trees.Length; i++)
            {
                string code;
                string why;
                Assert.True(NativeExpressionPrinter.TryPrint(trees[i], false, "Site" + i, out code, out why),
                    "tree " + i + " declined: " + why);
                source.Append(code.Replace("private static object", "public static object"));
            }

            source.Append("    }\n}\n");
            var compiled = CompileSource(source.ToString(), null);
            var sites = compiled.GetType("DifferentialSites.Sites");
            for (int i = 0; i < trees.Length; i++)
            {
                object expected = Expression.Lambda<Func<object, object, object, object>>(
                    Expression.Convert(trees[i], typeof(object)), model, chained, root)
                    .Compile()(null, null, null);
                object actual = sites.GetMethod("Site" + i).Invoke(null, new object[] { null, null, null });
                Assert.True(Equals(expected, actual),
                    "tree " + i + " (" + trees[i] + "): expected " + expected + " got " + actual);
                Assert.Equal(expected.GetType(), actual.GetType());
            }
        }

        private static T EvaluateConstant<T>(string literal)
        {
            string source = "namespace DifferentialConstants { public static class Holder { public static object Value() { return "
                + literal + "; } } }";
            var compiled = CompileSource(source, null);
            return (T)compiled.GetType("DifferentialConstants.Holder").GetMethod("Value").Invoke(null, null);
        }

        private void AssertTiersAgree(List<string> expressions) =>
            AssertTiersAgree(expressions, typeof(DifferentialModel), Model(), null);

        private void AssertTiersAgree(List<string> expressions, Type modelType, object model,
            Heddle.Runtime.Expressions.FunctionRegistry functions, bool wholeLines = false,
            System.Globalization.CultureInfo renderCulture = null)
        {
            var text = new StringBuilder();
            for (int i = 0; i < expressions.Count; i++)
            {
                if (wholeLines)
                    text.Append(i).Append(": ").Append(expressions[i]).Append('\n');
                else
                    text.Append(i).Append(": @(").Append(expressions[i]).Append(")\n");
            }

            string template = text.ToString();

            string key = "diff-" + Guid.NewGuid().ToString("N");
            string path = Path.Combine(_dir, key + ".heddle");
            File.WriteAllText(path, template);
            string rsp = Path.Combine(_dir, key + ".rsp");
            var arguments = new List<string>
            {
                "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native",
                "--template", path + "|" + key + "||" + modelType.AssemblyQualifiedName + "|",
                "--artifact-out", Out(key + ".bin"), "--source-out", Out(key + ".g.cs"),
                "--stamp", Out(key + ".txt")
            };
            if (functions != null)
            {
                // The exported functions are read off this assembly, so their call sites bind at build.
                arguments.Add("--reference");
                arguments.Add(modelType.Assembly.Location);
            }

            File.WriteAllLines(rsp, arguments);
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exit = Program.Run(new[] { "compile", "@" + rsp }, stdout, stderr);
            Assert.True(exit == 0, "exit " + exit + "\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);
            // Every site must be printed: a declined site rebuilds from data and would prove nothing
            // about the printer.
            Assert.True(!stdout.ToString().Contains("HED7031"), "a site was declined: " + stdout);

            var loaded = CompileSource(File.ReadAllText(Out(key + ".g.cs")), Out(key + ".bin"));
            Heddle.Precompiled.PrecompiledTemplates.Register(loaded);

            // The build above ran under the caller's culture; both tiers render under this one.
            if (renderCulture != null)
                System.Globalization.CultureInfo.CurrentCulture = renderCulture;
            var options = new Heddle.Data.TemplateOptions("differential");
            options.OutputProfile = Heddle.Data.OutputProfile.Text;
            if (functions != null)
                options.Functions = functions;
            var dynamicTier = new Heddle.HeddleTemplate(template,
                new Heddle.Runtime.CompileContext(options, new Heddle.Data.ExType(modelType)));
            Assert.True(dynamicTier.CompileResult.Success, dynamicTier.CompileResult.ToString());
            LazyLog.Entries.Clear();
            string expected = dynamicTier.Generate(model);
            var expectedReads = LazyLog.Entries.ToArray();

            AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", true);
            string actual;
            try
            {
                var strict = new Heddle.Data.TemplateOptions("strict-differential");
                if (functions != null)
                    strict.Functions = functions;
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = strict;
                var wrapper = loaded.GetType("Heddle.Generated." + SanitizeName.ForKey(key));
                Assert.NotNull(wrapper);
                var generate = wrapper.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(m => m.Name == "Generate" && m.ReturnType == typeof(string) &&
                                 m.GetParameters().Length == 3);
                LazyLog.Entries.Clear();
                actual = (string)generate.Invoke(null, new object[] { model, null, null });
            }
            finally
            {
                AppContext.SetSwitch("Heddle.Precompiled.StrictLoad", false);
                Heddle.Precompiled.PrecompiledTemplates.DefaultOptions = _savedDefaultOptions;
            }

            var expectedLines = expected.Split('\n');
            var actualLines = actual.Split('\n');
            Assert.Equal(expectedLines.Length, actualLines.Length);
            for (int i = 0; i < expressions.Count; i++)
            {
                Assert.True(expectedLines[i] == actualLines[i],
                    "@(" + expressions[i] + "): dynamic '" + expectedLines[i] + "' precompiled '" +
                    actualLines[i] + "'");
            }

            Assert.Equal(expected, actual);
            Assert.Equal(expectedReads, LazyLog.Entries.ToArray());
        }

        /// <summary>A sub-expression made only of constants is evaluated once, by the engine's rules, and
        /// printed as the value it gave — never as source for the C# compiler to fold again by its own rules.
        /// Pins the regressions where the two disagreed: constant <c>decimal</c> overflow is a C# compile error
        /// that <c>unchecked</c> does not lift (CS0463), and C# folds <c>int.MinValue / -1</c> to a value where
        /// the engine throws at render.</summary>
        [Fact]
        public void ConstantSubExpressionsPrintAsTheValueTheEngineComputed()
        {
            AssertTiersAgree(new List<string>
            {
                "A + (2 * 3 + 4)",
                "A + (7 / 2) + (7 % 4)",
                "D + (1.5 * 2)",
                "D + (1.0 / 3)",
                "A + (1 << 33)",
                "L + (2147483647 + 1)",
                "A > (10 - 1) ? \"big\" : \"small\"",
                "S ?? (\"a\" + \"b\" + 'c')",
                "A + (true ? 1 : 2)",
                "F == (1 < 2)",
                "A + (79228162514264337593543950335M - 1M > 0M ? 1 : 2)",
                "D + (0.1 + 0.2)",
                "A - (-(-5))",
                "A + (~7 & 12)"
            });
        }

        /// <summary>Text made from a number is made at render, under the render's culture — so a concatenation
        /// with a non-text constant is not a constant, however constant its operands. Pins the regression
        /// where the printer folded it under the <b>build machine's</b> culture: a build on a German host
        /// baked <c>x1,5</c> into a site the dynamic tier renders as <c>x1.5</c>.</summary>
        [Fact]
        public void TextMadeFromNumbersIsFormattedAtRenderNotAtBuild()
        {
            var saved = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                AssertTiersAgree(new List<string>
                {
                    "S + (\"x\" + 1.5)",
                    "(S ?? \"\") + (\"n\" + 2.5M) + (1.5 + 1)",
                    "\"d\" + (0 - 1.25) + S",
                    "S + (\"a\" + 'b' + \"c\")"
                }, typeof(DifferentialModel), Model(), null, false,
                    new System.Globalization.CultureInfo("en-US"));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = saved;
            }
        }

        /// <summary>A constant sub-expression the engine cannot evaluate — it throws — and an integral or
        /// decimal division by a constant zero have no C# spelling that compiles and behaves alike: the site
        /// is declined, rebuilt from data at load, and throws at render exactly as the dynamic tier does, only
        /// when it is reached.</summary>
        [Theory]
        [InlineData("A + (79228162514264337593543950335M + 1M)", typeof(OverflowException))]
        [InlineData("A + ((0 - 2147483647 - 1) / (0 - 1))", typeof(OverflowException))]
        [InlineData("A + ((0 - 2147483647 - 1) % (0 - 1))", null)]
        [InlineData("A / (1 - 1)", typeof(DivideByZeroException))]
        [InlineData("A % (2 - 2)", typeof(DivideByZeroException))]
        [InlineData("Price / (1M - 1M)", typeof(DivideByZeroException))]
        [InlineData("F ? A : A + (79228162514264337593543950335M + 1M)", null)]
        [InlineData("F ? A : A / (1 - 1)", null)]
        [InlineData("D / (1 - 1)", null)]
        public void ConstantsTheEngineCannotFoldDeclineTheSiteAndBehaveAlike(string expression, Type thrown)
        {
            string template = "v=@(" + expression + ")\n";
            string key = "fold-" + Guid.NewGuid().ToString("N");
            string path = Path.Combine(_dir, key + ".heddle");
            File.WriteAllText(path, template);
            string rsp = Path.Combine(_dir, key + ".rsp");
            File.WriteAllLines(rsp, new[]
            {
                "--project", Path.Combine(_dir, "proj.csproj"), "--root", _dir,
                "--output-profile", "Text", "--expression-mode", "Native",
                "--template", path + "|" + key + "||" + typeof(DifferentialModel).AssemblyQualifiedName + "|",
                "--artifact-out", Out(key + ".bin"), "--source-out", Out(key + ".g.cs"),
                "--stamp", Out(key + ".txt")
            });
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            int exit = Program.Run(new[] { "compile", "@" + rsp }, stdout, stderr);
            Assert.True(exit == 0, "exit " + exit + "\nSTDOUT:\n" + stdout + "\nSTDERR:\n" + stderr);

            var options = new Heddle.Data.TemplateOptions("fold") { OutputProfile = Heddle.Data.OutputProfile.Text };
            var dynamicTier = new Heddle.HeddleTemplate(template,
                new Heddle.Runtime.CompileContext(options, new Heddle.Data.ExType(typeof(DifferentialModel))));
            Assert.True(dynamicTier.CompileResult.Success, dynamicTier.CompileResult.ToString());

            // The printed source must compile whatever the engine made of the expression.
            var loaded = CompileSource(File.ReadAllText(Out(key + ".g.cs")), Out(key + ".bin"));
            Heddle.Precompiled.PrecompiledTemplates.Register(loaded);
            Heddle.Precompiled.PrecompiledTemplates.DefaultOptions =
                new Heddle.Data.TemplateOptions("fold-host") { OutputProfile = Heddle.Data.OutputProfile.Text };
            var generate = loaded.GetType("Heddle.Generated." + SanitizeName.ForKey(key))
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "Generate" && m.ReturnType == typeof(string) && m.GetParameters().Length == 3);
            var model = Model();
            Func<string> precompiled = () =>
            {
                try
                {
                    return (string)generate.Invoke(null, new object[] { model, null, null });
                }
                catch (TargetInvocationException e)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                    throw;
                }
            };

            string expected = Outcome(() => dynamicTier.Generate(model));
            string actual = Outcome(precompiled);
            Assert.Equal(expected, actual);
            if (thrown != null)
            {
                Assert.Equal("throws " + thrown.FullName, actual);
                Assert.Contains("HED7031", stdout.ToString());
            }
        }

        private static string Outcome(Func<string> render)
        {
            try
            {
                return "renders " + render();
            }
            catch (Exception e)
            {
                return "throws " + Innermost(e).GetType().FullName;
            }
        }

        private static Exception Innermost(Exception exception)
        {
            while (exception.InnerException != null)
                exception = exception.InnerException;
            return exception;
        }

        private static DifferentialModel Model() => new DifferentialModel
        {
            // Chosen so no sub-expression of the integer matrix is zero: a zero divisor would throw
            // on both tiers and compare nothing.
            A = 29,
            B = 14,
            C = 3,
            X = 100,
            L = 5000000000L,
            D = 2.5,
            N = null,
            F = true,
            G = false,
            H = false,
            S = null,
            Price = 12.5m,
            Max = int.MaxValue,
            Min = int.MinValue,
            BigL = long.MaxValue,
            Ch = '\'',
            Left = null,
            Right = new DifferentialLeaf { Name = "right", Numbers = new[] { 4, 5, 6 } },
            L1 = new List<string> { "one", "uno" },
            L2 = new List<string> { "two", "dos" }
        };

        private string Out(string name) => Path.Combine(_dir, "out", name);

        private static Assembly CompileSource(string source, string artifactPath)
        {
            var references = new List<MetadataReference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string location = null;
                try
                {
                    location = assembly.Location;
                }
                catch (NotSupportedException)
                {
                }

                if (string.IsNullOrEmpty(location) || !File.Exists(location) || !seen.Add(location))
                    continue;
                references.Add(MetadataReference.CreateFromFile(location));
            }

            var compilation = CSharpCompilation.Create("HeddleDifferential_" + Guid.NewGuid().ToString("N"),
                new[] { CSharpSyntaxTree.ParseText(source) }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release)
                    // A consumer may build with overflow checking on; the engine never checks.
                    .WithOverflowChecks(true));
            var image = new MemoryStream();
            var resources = artifactPath == null
                ? null
                : new[]
                {
                    new ResourceDescription("Heddle.CompiledForm", () => File.OpenRead(artifactPath), true)
                };
            var emit = compilation.Emit(image, manifestResources: resources);
            Assert.True(emit.Success,
                "Printed source did not compile: " + string.Join("\n",
                    emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                        .Select(d => d.ToString()).ToArray()) + "\n" + source);
            return Assembly.Load(image.ToArray());
        }
    }
}
