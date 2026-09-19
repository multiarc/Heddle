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


        /// <summary>Re-lays the container with one section's bytes replaced. The digest is not
        /// recomputed: every structural check below fires before the digest is verified.</summary>
        private static byte[] ReplaceSection(byte[] image, int id, byte[] blob)
        {
            int count = (int)System.BitConverter.ToUInt32(image, 8);
            var ids = new System.Collections.Generic.List<int>();
            var blobs = new System.Collections.Generic.List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                int entry = 12 + i * 10;
                int sectionId = image[entry] | (image[entry + 1] << 8);
                int offset = (int)System.BitConverter.ToUInt32(image, entry + 2);
                int length = (int)System.BitConverter.ToUInt32(image, entry + 6);
                var data = new byte[length];
                System.Buffer.BlockCopy(image, offset, data, 0, length);
                ids.Add(sectionId);
                blobs.Add(sectionId == id ? blob : data);
            }

            var output = new System.Collections.Generic.List<byte>();
            for (int i = 0; i < 12; i++)
                output.Add(image[i]);
            int cursor = 12 + count * 10;
            for (int i = 0; i < count; i++)
            {
                output.Add((byte)ids[i]);
                output.Add((byte)(ids[i] >> 8));
                output.AddRange(System.BitConverter.GetBytes((uint)cursor));
                output.AddRange(System.BitConverter.GetBytes((uint)blobs[i].Length));
                cursor += blobs[i].Length;
            }
            foreach (var data in blobs)
                output.AddRange(data);
            return output.ToArray();
        }

        /// <summary>A varint whose tenth byte carries more than one bit overflows a ulong; before the
        /// check the surplus bits were shifted away and the value decoded as something else.</summary>
        [Fact]
        public void VarintOverflowingAULongIsMalformed()
        {
            byte[] good = CompiledFormWriter.Write(CompiledFormHarness.MinimalArtifact());
            var strings = new System.Collections.Generic.List<byte>();
            for (int i = 0; i < 9; i++)
                strings.Add(0x80);
            strings.Add(0x7F);
            var fault = Assert.Throws<InvalidDataException>(
                () => CompiledFormReader.Read(ReplaceSection(good, SectionIds.Strings, strings.ToArray())));
            Assert.Contains("varint overflows", fault.Message);
        }

        private static CompiledExpression Nested(int depth)
        {
            var node = new CompiledExpression { Kind = CompiledExprKind.This, Position = new CompiledPosition(0, 0) };
            for (int i = 0; i < depth; i++)
                node = new CompiledExpression
                {
                    Kind = CompiledExprKind.Unary,
                    Position = new CompiledPosition(0, 0),
                    Operator = CompiledExprOperator.Add,
                    Operand = node
                };
            return node;
        }

        [Fact]
        public void NestingAtTheBoundRoundTripsAndOneDeeperIsRefusedByTheWriter()
        {
            var artifact = CompiledFormHarness.MinimalArtifact();
            artifact.Expressions.Add(new CompiledExpressionTree { Root = Nested(CompiledFormLimits.MaxNesting - 1) });
            var back = CompiledFormReader.Read(CompiledFormWriter.Write(artifact));
            int depth = 0;
            for (var node = back.Expressions[0].Root; node.Kind == CompiledExprKind.Unary; node = node.Operand)
                depth++;
            Assert.Equal(CompiledFormLimits.MaxNesting - 1, depth);

            var deeper = CompiledFormHarness.MinimalArtifact();
            deeper.Expressions.Add(new CompiledExpressionTree { Root = Nested(CompiledFormLimits.MaxNesting) });
            var fault = Assert.Throws<System.InvalidOperationException>(() => CompiledFormWriter.Write(deeper));
            Assert.Contains("nesting exceeds", fault.Message);
        }

        /// <summary>The reader's own bound, reached through bytes the writer refuses to produce: a
        /// unary chain one level past the limit is malformed, not a stack overflow.</summary>
        [Fact]
        public void NestingBeyondTheBoundIsMalformedAtLoad()
        {
            byte[] good = CompiledFormWriter.Write(CompiledFormHarness.MinimalArtifact());
            var blob = new System.Collections.Generic.List<byte> { 1 };
            for (int i = 0; i <= CompiledFormLimits.MaxNesting; i++)
            {
                blob.Add((byte)CompiledExprKind.Unary);
                blob.Add(0);
                blob.Add(0);
                blob.Add((byte)CompiledExprOperator.Add);
            }
            blob.Add((byte)CompiledExprKind.This);
            blob.Add(0);
            blob.Add(0);
            blob.Add(0);
            blob.Add(0);
            blob.Add(0);
            blob.Add(0);
            var fault = Assert.Throws<InvalidDataException>(
                () => CompiledFormReader.Read(ReplaceSection(good, SectionIds.Expressions, blob.ToArray())));
            Assert.Contains("Nesting exceeds", fault.Message);
        }

        [Fact]
        public void ImportOnlyRowRoundTripsAndIsNotRegistered()
        {
            var artifact = CompiledFormHarness.MinimalArtifact();
            artifact.Documents[0].RawText = "<greet>{{Hi}}";
            var row = CompiledFormHarness.TemplateRow("importonly/partial.heddle");
            row.IsImportOnly = true;
            artifact.Templates.Add(row);
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("importonly/page.heddle"));
            var back = CompiledFormReader.Read(CompiledFormWriter.Write(artifact));
            Assert.True(back.Templates[0].IsImportOnly);
            Assert.False(back.Templates[1].IsImportOnly);

            CompiledFormHarness.RegisterArtifact(artifact, "HeddleTestAsm_ImportOnly");
            Assert.False(PrecompiledTemplates.TryGet("importonly/partial.heddle", out _));
            Assert.True(PrecompiledTemplates.TryGet("importonly/page.heddle", out _));
        }

        /// <summary>Idempotence is per assembly instance: registering the same assembly twice is a
        /// no-op, while a different assembly with the same simple name is a real registration whose
        /// duplicate key is diagnosed instead of the second assembly being dropped silently.</summary>
        [Fact]
        public void SameSimpleNameFromAnotherAssemblyIsNotSilentlySkipped()
        {
            var artifact = CompiledFormHarness.MinimalArtifact();
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("identity/one.heddle"));
            byte[] image = CompiledFormWriter.Write(artifact);
            var first = CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_SameName", exactName: true);
            PrecompiledTemplates.Register(first);
            Assert.True(PrecompiledTemplates.TryGet("identity/one.heddle", out _));

            var fault = Assert.Throws<PrecompiledRegistrationException>(
                () => CompiledFormHarness.RegisterImage(image, "HeddleTestAsm_SameName", exactName: true));
            Assert.Contains("identity/one.heddle", fault.Message);
        }

        /// <summary>The gauntlet checks the member rows a template's own sites reference, not the whole
        /// merged table: a row another template recorded cannot fail this one.</summary>
        [Fact]
        public void MemberRowsBelongToTheTemplateWhoseSitesReferenceThem()
        {
            var artifact = CompiledFormHarness.MinimalArtifact();
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("owned/a.heddle"));
            artifact.Templates.Add(CompiledFormHarness.TemplateRow("owned/b.heddle"));
            artifact.Members.Add(new CompiledMemberRow
            {
                StartType = CompiledFormHarness.TypeRef(typeof(string)),
                Segments = { "Length" },
                Hops = { new CompiledMemberHop { DeclaringType = CompiledFormHarness.TypeRef(typeof(string)), MemberName = "Length", MemberType = CompiledFormHarness.TypeRef(typeof(int)) } }
            });
            artifact.Members.Add(new CompiledMemberRow
            {
                StartType = CompiledFormHarness.TypeRef("Nope.Missing", "NopeAssembly"),
                Segments = { "Gone" },
                Hops = { new CompiledMemberHop { DeclaringType = CompiledFormHarness.TypeRef("Nope.Missing", "NopeAssembly"), MemberName = "Gone", MemberType = CompiledFormHarness.TypeRef(typeof(int)) } }
            });
            artifact.Sites.Add(new CompiledSiteRow { TemplateIndex = 0, SiteOrdinal = 0, Kind = CompiledSiteKind.MemberAccessor, PayloadRef = 0 });
            artifact.Sites.Add(new CompiledSiteRow { TemplateIndex = 1, SiteOrdinal = 0, Kind = CompiledSiteKind.MemberAccessor, PayloadRef = 1 });

            var a = CompiledFormHarness.LoaderRow(artifact, 0);
            var b = CompiledFormHarness.LoaderRow(artifact, 1);
            Assert.Equal("Length", Assert.Single(a.MemberRows).Segments[0]);
            Assert.Equal("Gone", Assert.Single(b.MemberRows).Segments[0]);
        }
    }
}
