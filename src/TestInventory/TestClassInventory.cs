using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Heddle.TestInventory
{
    /// <summary>
    /// The per-suite test-class inventory: a checked-in list of every type in a test assembly that declares an
    /// xUnit fact, asserted by <b>set equality</b> against what the built assembly actually carries.
    /// <para>This replaces the hand-maintained <c>--minimum-expected-tests</c> floors nine CI legs used to pass.
    /// The floors were meant to notice a suite going quiet and could not: a floor is satisfied by editing a digit,
    /// it names nothing when it reddens, it has to be raised by hand in the same commit a suite grows, and it fails
    /// a leg for the wrong reason whenever a test is legitimately quarantined. They also drifted in practice — one
    /// commit added six tests, raised the floor in one workflow and not the other, and the second workflow then
    /// tolerated a six-test regression in silence for four commits.</para>
    /// <para>A set difference cannot do any of that: making this gate green again requires naming the class that
    /// appeared or vanished, which is the review artifact the count was reaching for and structurally could not
    /// produce. It is the same shape the corpus membership gate already uses
    /// (<c>src/TestCorpus/CorpusIntent.cs</c>), deliberately.</para>
    /// <para><b>What it does not catch, stated plainly:</b> a single <c>[Fact]</c> deleted from a class that still
    /// declares others, a <c>[Theory]</c> losing rows, or a test body emptied of its assertions. The unit of this
    /// gate is the class, and it is worth having only because the class is the unit a whole file of coverage
    /// disappears in — a suite silently losing a class is the failure that actually happened here. Nothing about it
    /// claims to measure how much a suite tests.</para>
    /// </summary>
    internal static class TestClassInventory
    {
        /// <summary>The inventory's name in each suite's project directory. In the output directory it is
        /// qualified by assembly name (see <see cref="PathFor"/>), so a suite that copies another suite's build
        /// output alongside its own cannot overwrite it.</summary>
        internal const string FileName = "test-classes.txt";

        /// <summary>The built copy of one assembly's inventory.</summary>
        internal static string PathFor(Assembly assembly) =>
            Path.Combine(AppContext.BaseDirectory, "TestInventory",
                assembly.GetName().Name + "." + FileName);

        /// <summary>Every type in <paramref name="assembly"/> that would produce at least one test: a concrete
        /// class carrying a <c>[Fact]</c> or <c>[Theory]</c> (<c>TheoryAttribute</c> derives from
        /// <c>FactAttribute</c>, so one test covers both), inherited members included. Ordinal-sorted, nested types
        /// spelled with the <c>+</c> the runtime uses.</summary>
        internal static IReadOnlyList<string> DeclaringTypes(Assembly assembly)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Instance | BindingFlags.Static;

            var names = new List<string>();
            foreach (var type in LoadableTypes(assembly))
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition || type.FullName == null)
                    continue;
                if (type.GetMethods(flags).Any(m => m.GetCustomAttributes(typeof(FactAttribute), true).Length != 0))
                    names.Add(type.FullName);
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        /// <summary>Asserts the assembly's fact-declaring classes are exactly the ones checked in.
        /// <paramref name="sourcePath"/> is the repository path of the file to edit, named in every failure so the
        /// message says what to do as well as what happened.</summary>
        internal static void AssertMatchesCheckedInInventory(Assembly assembly, string sourcePath)
        {
            var observed = DeclaringTypes(assembly);
            var built = PathFor(assembly);
            var actual = Path.ChangeExtension(built, ".actual.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(built));
            File.WriteAllText(actual, string.Join("\n", observed) + "\n");

            Assert.True(File.Exists(built),
                "The test-class inventory for " + assembly.GetName().Name + " is missing (" + sourcePath +
                "). This assembly's current classes were written to '" + actual + "'.");

            var declared = ReadInventory(built);

            var duplicates = declared.GroupBy(n => n, StringComparer.Ordinal).Where(g => g.Count() > 1)
                .Select(g => g.Key).ToList();
            Assert.True(duplicates.Count == 0,
                sourcePath + " lists the same class more than once: " + string.Join(", ", duplicates));

            var sorted = declared.OrderBy(n => n, StringComparer.Ordinal).ToList();
            Assert.True(declared.SequenceEqual(sorted, StringComparer.Ordinal),
                sourcePath + " is not ordinal-sorted, so its diffs do not read as additions and removals. " +
                "The sorted list of what this assembly carries is in '" + actual + "'.");

            var declaredSet = new HashSet<string>(declared, StringComparer.Ordinal);
            Assert.True(declaredSet.SetEquals(observed), Describe(assembly, sourcePath, declared, observed, actual));
        }

        /// <summary>Formats the symmetric difference — the whole point of the gate. A class that vanished is a
        /// class whose tests stopped running, and it is named here rather than left as a number that moved.</summary>
        private static string Describe(Assembly assembly, string sourcePath, IEnumerable<string> declared,
            IEnumerable<string> observed, string actual)
        {
            var d = new SortedSet<string>(declared, StringComparer.Ordinal);
            var o = new SortedSet<string>(observed, StringComparer.Ordinal);
            var vanished = d.Except(o).ToList();
            var appeared = o.Except(d).ToList();
            return "The test-class inventory for " + assembly.GetName().Name + " drifted from " + sourcePath + "."
                   + "\n  Checked in but NOT in the assembly (" + vanished.Count + "): "
                   + (vanished.Count == 0 ? "(none)" : string.Join(", ", vanished))
                   + "\n  In the assembly but NOT checked in (" + appeared.Count + "): "
                   + (appeared.Count == 0 ? "(none)" : string.Join(", ", appeared))
                   + "\n  A class that vanished is a class whose tests stopped running: put it back, or delete its"
                   + " line and say in the commit message where its coverage went."
                   + "\n  The current list is in '" + actual + "'.";
        }

        /// <summary>Reads the inventory: one class per line, blank lines and <c>#</c> comments ignored, line
        /// endings normalised so a Windows checkout reads the same list a Linux one does.</summary>
        private static IReadOnlyList<string> ReadInventory(string path)
        {
            var lines = new List<string>();
            foreach (var raw in File.ReadAllText(path).Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                lines.Add(line);
            }

            return lines;
        }

        private static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // A type that will not load is itself drift the set difference reports, so the partial answer is
                // the useful one — throwing here would replace a named class with a stack trace.
                return e.Types.Where(t => t != null);
            }
        }
    }
}
