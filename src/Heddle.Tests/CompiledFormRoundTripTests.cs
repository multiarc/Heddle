using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Heddle.Precompiled;
using Heddle.Precompiled.CompiledForm;
using Heddle.Runtime;
using Heddle.TestCorpus;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>Artifact round-trip (P1-W9 exit criterion 2): write/read/write is byte-deterministic,
    /// refusal sets survive the trip exactly, and every document carries the raw text the loader parses
    /// (empty only for synthesized fragment documents, which the loader never parses).
    /// Serialized — the registry is process-global static state.</summary>
    [Collection("PrecompiledRegistrySerial")]
    public class CompiledFormRoundTripTests : IDisposable
    {
        private readonly Action<PrecompiledFallbackEvent> _savedCallback;

        public CompiledFormRoundTripTests()
        {
            _savedCallback = PrecompiledTemplates.OnFallback;
            PrecompiledTemplates.ResetForTests();
            CorpusExtensionFixtures.Register();
        }

        public void Dispose()
        {
            PrecompiledTemplates.OnFallback = _savedCallback;
            PrecompiledTemplates.ResetForTests();
        }

        [Fact]
        public void WriteReadWriteIsByteDeterministic()
        {
            // A blank template, an escape-heavy one, a definition-heavy one and a refusal row: the
            // shapes most likely to wobble across the codec.
            foreach (var name in new[] {
                "at-escape.heddle", "props-defaults.heddle", "ext-site-fallback.heddle", "ctx-encoding.heddle" })
            {
                var row = Find(name);
                string text = CompiledFormHarness.CorpusText(name);
                var options = CompiledFormHarness.RowOptions(name, row, TestCorpusIndex.CorpusDir);
                Type modelType;
                object model;
                var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
                CompileContext context;
                var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
                Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                    "Build failed for " + name + ": " + CompiledFormHarness.Summarize(context) + ".");
                var artifact = CompiledFormHarness.ToArtifact(context, name, text, modelEx, options, row);
                byte[] first = CompiledFormWriter.Write(artifact);
                byte[] second = CompiledFormWriter.Write(CompiledFormReader.Read(first));
                Assert.True(first.SequenceEqual(second),
                    "Write/read/write diverged for " + name + " (" + first.Length + " vs " + second.Length + " bytes).");
            }
        }

        [Fact]
        public void EveryParsedDocumentCarriesItsRawText()
        {
            foreach (var row in CorpusIntent.Rows)
            {
                if (!row.Bound || CompiledFormHarness.IsUnresolvableRow(row.Name))
                    continue;
                PrecompiledTemplates.ResetForTests();
                string text = CompiledFormHarness.CorpusText(row.Name);
                var options = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
                Type modelType;
                object model;
                var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
                CompileContext context;
                var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
                Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                    "Build failed for " + row.Name + ".");
                var artifact = CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, options, row);
                foreach (var document in artifact.Documents)
                    Assert.False(string.IsNullOrEmpty(document.RawText) && document.Elements.Count != 0,
                        "Document without raw text carries elements in " + row.Name + ".");
                var back = CompiledFormReader.Read(CompiledFormWriter.Write(artifact));
                for (int i = 0; i < artifact.Documents.Count; i++)
                    Assert.Equal(artifact.Documents[i].RawText, back.Documents[i].RawText);
            }
        }

        private static CorpusIntentRow Find(string name)
        {
            foreach (var row in CorpusIntent.Rows)
                if (string.Equals(row.Name, name, StringComparison.Ordinal))
                    return row;
            throw new InvalidOperationException("No intent row names '" + name + "'.");
        }
        /// <summary>AC-3: a malformed container — bad magic, an unreadable schema, a section table that
        /// runs past the image — surfaces from Register as PrecompiledRegistrationException naming the
        /// assembly, with the reader's InvalidDataException inside; never as the reader's exception.</summary>
        [Fact]
        public void MalformedContainerRegistersAsRegistrationFaultNamingTheAssembly()
        {
            byte[] good = CompiledFormWriter.Write(CompiledFormHarness.MinimalArtifact());
            var cases = new List<KeyValuePair<string, byte[]>>();
            var badMagic = (byte[])good.Clone();
            badMagic[0] ^= 0xFF;
            cases.Add(new KeyValuePair<string, byte[]>("bad magic", badMagic));
            var badSchema = (byte[])good.Clone();
            badSchema[4] = 0xFF;
            badSchema[5] = 0xFF;
            cases.Add(new KeyValuePair<string, byte[]>("unreadable schema", badSchema));
            var truncated = new byte[Math.Min(good.Length, 40)];
            Array.Copy(good, truncated, truncated.Length);
            cases.Add(new KeyValuePair<string, byte[]>("section table past the end", truncated));
            // A required section missing: renumber the Strings section's table entry (id 2) to an unknown
            // id, which the reader skips by length, so the required id is never seen.
            var missing = (byte[])good.Clone();
            uint sectionCount = BitConverter.ToUInt32(missing, 8);
            bool renumbered = false;
            for (int i = 0; i < sectionCount; i++)
            {
                int entry = 12 + i * 10;
                if (missing[entry] == 2 && missing[entry + 1] == 0)
                {
                    missing[entry] = 200;
                    renumbered = true;
                    break;
                }
            }
            Assert.True(renumbered, "The minimal artifact should carry a Strings section to renumber.");
            cases.Add(new KeyValuePair<string, byte[]>("required section missing", missing));
            foreach (var c in cases)
            {
                PrecompiledTemplates.ResetForTests();
                var fault = Assert.Throws<PrecompiledRegistrationException>(
                    () => { CompiledFormHarness.RegisterImage(c.Value, "HeddleTestAsm_Malformed"); });
                Assert.StartsWith("HeddleTestAsm_Malformed", fault.NewAssemblyName);
                Assert.IsType<InvalidDataException>(fault.InnerException);
                Assert.Contains("malformed", fault.Message);
                Assert.Contains(fault.NewAssemblyName, fault.Message);
            }
        }

        /// <summary>AC-6: a refusal-class site is a row in Sites at its walk ordinal, its payload the
        /// refusal's index in the row's RefusalSites list, and RefusalSites carries that ordinal.</summary>
        [Theory]
        [InlineData("ext-site-fallback.heddle", 1)]
        [InlineData("ext-site-fallback-twice.heddle", 2)]
        public void RefusalSiteIsASiteRowAtItsWalkOrdinal(string name, int expectedRefusals)
        {
            var row = Find(name);
            Assert.True(row.Refusals.Count > 0, name + " should declare a refusal.");
            string text = CompiledFormHarness.CorpusText(row.Name);
            var options = CompiledFormHarness.RowOptions(row.Name, row, TestCorpusIndex.CorpusDir);
            Type modelType;
            object model;
            var modelEx = CompiledFormHarness.ModelExFor(row, out modelType, out model);
            CompileContext context;
            var template = CompiledFormHarness.BuildRecording(text, options, modelEx, out context);
            Assert.True(template.CompileResult.Success && context.CompileErrors.Count == 0,
                "Build failed: " + CompiledFormHarness.Summarize(context) + ".");
            byte[] image = CompiledFormWriter.Write(
                CompiledFormHarness.ToArtifact(context, row.Name, text, modelEx, options, row));
            var artifact = CompiledFormReader.Read(image);
            Assert.Equal(image, CompiledFormWriter.Write(artifact));
            var templateRow = Assert.Single(artifact.Templates);
            Assert.Equal(expectedRefusals, templateRow.RefusalSites.Count);
            Assert.Single(artifact.Documents);
            var refusalRows = artifact.Sites.Where(s => s.Kind == CompiledSiteKind.Refusal).ToList();
            Assert.Equal(templateRow.RefusalSites.Count, refusalRows.Count);
            var ordinals = new HashSet<int>();
            for (int i = 0; i < templateRow.RefusalSites.Count; i++)
            {
                var site = templateRow.RefusalSites[i];
                var siteRow = Assert.Single(refusalRows, r => r.PayloadRef == i);
                Assert.Equal(siteRow.SiteOrdinal, site.SiteOrdinal);
                Assert.Equal(CompiledSiteKind.Refusal, artifact.Sites[site.SiteOrdinal].Kind);
                Assert.True(ordinals.Add(site.SiteOrdinal), "Refusal sites must carry distinct walk ordinals.");
            }
        }

    }
}
