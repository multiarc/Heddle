using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>Decodes a compiled-form artifact image back to its rows. Stateless and thread-safe. Any
    /// structural defect — bad magic, an unsupported schema, a broken section table, a digest mismatch
    /// or a truncated section — reports as <see cref="InvalidDataException"/>; the registration layer
    /// maps that to its own fault. Unknown section ids are skipped by length.</summary>
    public static class CompiledFormReader
    {
        /// <summary>Decodes a complete artifact image.</summary>
        public static CompiledArtifact Read(byte[] image)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            return new Decoder(image).ReadArtifact();
        }

        /// <summary>Reads <paramref name="source"/> fully, then decodes it. The stream is left open.</summary>
        public static CompiledArtifact Read(Stream source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            using (var buffer = new MemoryStream())
            {
                source.CopyTo(buffer);
                return Read(buffer.ToArray());
            }
        }

        private sealed class Decoder
        {
            private readonly byte[] _image;
            private int _offset;
            private int _limit;
            private List<string> _strings;
            private List<CompiledTypeRef> _types;

            internal Decoder(byte[] image)
            {
                _image = image;
            }

            internal CompiledArtifact ReadArtifact()
            {
                var blobs = ReadContainer();
                var artifact = new CompiledArtifact();
                ReadStringsInto(blobs[SectionIds.Strings]);
                ReadTypesInto(blobs[SectionIds.Types]);
                artifact.Header = ReadHeader(blobs[SectionIds.Header]);
                ReadExtensionsInto(artifact, blobs[SectionIds.Extensions]);
                ReadFunctionsInto(artifact, blobs[SectionIds.Functions]);
                ReadMembersInto(artifact, blobs[SectionIds.Members]);
                ReadExpressionsInto(artifact, blobs[SectionIds.Expressions]);
                ReadCSharpInto(artifact, blobs[SectionIds.CSharp]);
                ReadDocumentsInto(artifact, blobs[SectionIds.Documents]);
                ReadDefinitionsInto(artifact, blobs[SectionIds.Definitions]);
                ReadTemplatesInto(artifact, blobs[SectionIds.Templates]);
                ReadSitesInto(artifact, blobs[SectionIds.Sites]);
                artifact.DigestHex = VerifyDigest(blobs);
                ValidateRefs(artifact);
                if (artifact.Header == null)
                    Malformed("The header section is empty.");
                if (artifact.Templates.Count != _headerTemplateCount)
                    Malformed("The header template count does not match the template rows.");
                return artifact;
            }

            private static InvalidDataException Malformed(string detail)
            {
                throw new InvalidDataException("The compiled-form artifact is malformed: " + detail);
            }

            private byte[][] ReadContainer()
            {
                if (_image.Length < 12)
                    Malformed("The image is shorter than the container header.");
                if (_image[0] != (byte)'H' || _image[1] != (byte)'C' ||
                    _image[2] != (byte)'F' || _image[3] != (byte)'3')
                    Malformed("The magic is not 'HCF3'.");
                uint schema = ReadU32At(4);
                if (schema != (uint)PrecompiledSchema.CompiledFormSchemaVersion)
                    Malformed("Schema " + schema + " is not readable; schema " +
                        PrecompiledSchema.CompiledFormSchemaVersion + " is required.");
                uint sectionCount = ReadU32At(8);
                if (sectionCount > 64)
                    Malformed("The section count is not plausible.");
                long tableEnd = 12L + (long)sectionCount * 10;
                if (tableEnd > _image.Length)
                    Malformed("The section table runs past the end of the image.");

                var blobs = new byte[SectionIds.RequiredCount + 1][];
                var seen = new bool[SectionIds.RequiredCount + 1];
                var spans = new List<long[]>();
                for (uint i = 0; i < sectionCount; i++)
                {
                    int entry = 12 + (int)i * 10;
                    int id = _image[entry] | (_image[entry + 1] << 8);
                    uint offset = ReadU32At(entry + 2);
                    uint length = ReadU32At(entry + 6);
                    if ((long)offset + length > _image.Length)
                        Malformed("A section runs past the end of the image.");
                    if (id >= SectionIds.First && id <= SectionIds.Last)
                    {
                        if (seen[id])
                            Malformed("A section id appears twice.");
                        seen[id] = true;
                        var blob = new byte[length];
                        Buffer.BlockCopy(_image, (int)offset, blob, 0, (int)length);
                        blobs[id] = blob;
                        _sectionOffsets[id] = (int)offset;
                    }
                    spans.Add(new long[] { offset, offset + length });
                }
                spans.Sort((a, b) => a[0].CompareTo(b[0]));
                for (int i = 1; i < spans.Count; i++)
                    if (spans[i][0] < spans[i - 1][1])
                        Malformed("Two sections overlap.");
                for (int id = SectionIds.First; id <= SectionIds.Last; id++)
                    if (!seen[id])
                        Malformed("Required section " + id + " is missing.");
                return blobs;
            }

            private uint ReadU32At(int offset)
            {
                return (uint)(_image[offset] | (_image[offset + 1] << 8) |
                    (_image[offset + 2] << 16) | (_image[offset + 3] << 24));
            }

            private void Open(byte[] blob)
            {
                _offset = 0;
                _limit = blob.Length;
                _current = blob;
            }

            private byte[] _current;
            private int _headerTemplateCount;
            private readonly int[] _sectionOffsets = new int[SectionIds.RequiredCount + 1];

            private void Need(int count)
            {
                if (count < 0 || (long)_offset + count > _limit)
                    Malformed("A section ends mid-value.");
            }

            private byte ReadByte()
            {
                Need(1);
                return _current[_offset++];
            }

            private bool ReadBool()
            {
                byte value = ReadByte();
                if (value > 1)
                    Malformed("A boolean is not 0 or 1.");
                return value == 1;
            }

            private ulong ReadUVarint()
            {
                ulong value = 0;
                int shift = 0;
                while (true)
                {
                    byte b = ReadByte();
                    if (shift >= 64 && (b & 0x7F) != 0)
                        Malformed("A varint overflows.");
                    value |= (ulong)(b & 0x7F) << shift;
                    if ((b & 0x80) == 0)
                        return value;
                    shift += 7;
                }
            }

            private int ReadCount()
            {
                ulong value = ReadUVarint();
                if (value > int.MaxValue)
                    Malformed("A count is out of range.");
                int count = (int)value;
                if (count > _limit - _offset)
                    Malformed("A count exceeds the bytes left in the section.");
                return count;
            }

            private int ReadIndex(int count, string what)
            {
                int index = ReadSized();
                if (index >= count)
                    Malformed("A " + what + " index is out of range.");
                return index;
            }

            private int ReadSized()
            {
                ulong value = ReadUVarint();
                if (value > int.MaxValue)
                    Malformed("A value is out of range.");
                return (int)value;
            }

            private long ReadSVarint64()
            {
                ulong raw = ReadUVarint();
                return (long)(raw >> 1) ^ -((long)(raw & 1));
            }

            private double ReadDouble()
            {
                Need(8);
                long bits = 0;
                for (int i = 0; i < 8; i++)
                    bits |= (long)_current[_offset++] << (8 * i);
                return BitConverter.Int64BitsToDouble(bits);
            }

            private float ReadFloat()
            {
                Need(4);
                int bits = 0;
                for (int i = 0; i < 4; i++)
                    bits |= _current[_offset++] << (8 * i);
                return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }

            private decimal ReadDecimal()
            {
                Need(16);
                var parts = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    int part = 0;
                    for (int b = 0; b < 4; b++)
                        part |= _current[_offset++] << (8 * b);
                    parts[i] = part;
                }
                try
                {
                    return new decimal(parts);
                }
                catch (ArgumentException)
                {
                    throw Malformed("A decimal encoding is not valid.");
                }
            }

            private string ReadStringValue()
            {
                int index = ReadSized();
                if (index >= _strings.Count)
                    Malformed("A string index is out of range.");
                return _strings[index];
            }

            private string ReadOptStringValue()
            {
                if (!ReadBool())
                    return null;
                return ReadStringValue();
            }

            private CompiledTypeRef ReadTypeRefValue()
            {
                if (!ReadBool())
                    return null;
                int index = ReadSized();
                if (index >= _types.Count)
                    Malformed("A type index is out of range.");
                return _types[index];
            }

            private CompiledPosition ReadPosition()
            {
                int start = ReadSized();
                int length = ReadSized();
                return new CompiledPosition(start, length);
            }

            private CompiledLiteral ReadLiteralBody()
            {
                byte kind = ReadByte();
                if (kind > (byte)CompiledLiteralKind.Char)
                    Malformed("A literal kind is out of range.");
                var literal = new CompiledLiteral { Kind = (CompiledLiteralKind)kind };
                switch (literal.Kind)
                {
                    case CompiledLiteralKind.Null:
                        break;
                    case CompiledLiteralKind.Int64:
                        literal.Int64 = ReadSVarint64();
                        break;
                    case CompiledLiteralKind.UInt64:
                        literal.UInt64 = ReadUVarint();
                        break;
                    case CompiledLiteralKind.Float:
                        literal.Float32 = ReadFloat();
                        break;
                    case CompiledLiteralKind.Double:
                        literal.Double = ReadDouble();
                        break;
                    case CompiledLiteralKind.Decimal:
                        literal.Decimal = ReadDecimal();
                        break;
                    case CompiledLiteralKind.Boolean:
                        literal.Boolean = ReadBool();
                        break;
                    case CompiledLiteralKind.String:
                        literal.Text = ReadStringValue();
                        break;
                    case CompiledLiteralKind.Char:
                        literal.CharCode = ReadSized();
                        if (literal.CharCode > 0xFFFF)
                            Malformed("A char code is out of range.");
                        break;
                }
                return literal;
            }

            private CompiledLiteral ReadOptLiteral() => ReadBool() ? ReadLiteralBody() : null;

            private void ExpectEnd()
            {
                if (_offset != _limit)
                    Malformed("A section has trailing bytes.");
            }

            private void ReadStringsInto(byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                _strings = new List<string>(count);
                for (int i = 0; i < count; i++)
                {
                    int units = ReadCount();
                    if ((long)units * 2 > _limit - _offset)
                        Malformed("A string runs past the end of the section.");
                    var chars = new char[units];
                    for (int c = 0; c < units; c++)
                    {
                        int low = _current[_offset++];
                        int high = _current[_offset++];
                        chars[c] = (char)(low | (high << 8));
                    }
                    _strings.Add(new string(chars));
                }
                ExpectEnd();
            }

            private void ReadTypesInto(byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                _types = new List<CompiledTypeRef>(count);
                for (int i = 0; i < count; i++)
                    _types.Add(ReadTypeEntry());
                ExpectEnd();
                foreach (var typeRef in _types)
                {
                    var generic = typeRef as GenericTypeRef;
                    if (generic == null)
                        continue;
                    for (int i = 0; i < generic.Arguments.Count; i++)
                        if (generic.Arguments[i] == null)
                            Malformed("A generic type argument is missing.");
                }
            }

            private CompiledTypeRef ReadTypeEntry()
            {
                byte kind = ReadByte();
                if (kind > 3)
                    Malformed("A type kind is out of range.");
                if (kind == 0)
                    return new NamedTypeRef(ReadStringValue(), ReadStringValue(), ReadBool());
                if (kind == 2)
                {
                    int element = ReadSized();
                    int rank = ReadSized();
                    if (rank < 1)
                        Malformed("An array rank is less than 1.");
                    if (element >= _types.Count)
                        Malformed("An array element index is out of range.");
                    return new ArrayTypeRef(_types[element], rank);
                }
                if (kind == 3)
                    return DynamicTypeRef.Instance;
                var definition = new NamedTypeRef(ReadStringValue(), ReadStringValue(), ReadBool());
                int argumentCount = ReadCount();
                var arguments = new List<CompiledTypeRef>(argumentCount);
                for (int i = 0; i < argumentCount; i++)
                {
                    int index = ReadSized();
                    arguments.Add(index < _types.Count ? _types[index] : null);
                }
                return new GenericTypeRef(definition, arguments);
            }

            private CompiledHeader ReadHeader(byte[] blob)
            {
                Open(blob);
                if (blob.Length < 32)
                    Malformed("The header section is too short for a digest.");
                var header = new CompiledHeader
                {
                    EngineVersion = ReadStringValue(),
                    BuilderVersion = ReadStringValue(),
                    ExpressionMode = ReadStringValue()
                };
                header.TrimDirectiveLines = ReadBool();
                header.DefaultOutputProfile = ReadOptStringValue();
                _headerTemplateCount = ReadCount();
                if (_limit - _offset != 32)
                    Malformed("The header section does not end with a 32-byte digest.");
                _offset = _limit;
                return header;
            }

            private void ReadExtensionsInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    artifact.Extensions.Add(new CompiledExtensionRow
                    {
                        RegistryName = ReadStringValue(),
                        Type = ReadTypeRefValue(),
                        Fingerprint = ReadOptStringValue()
                    });
                    if (artifact.Extensions[i].Type == null)
                        Malformed("An extension type is missing.");
                }
                ExpectEnd();
            }

            private void ReadFunctionsInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    artifact.Functions.Add(new CompiledFunctionRow
                    {
                        Name = ReadStringValue(),
                        Target = ReadTypeRefValue(),
                        OverloadCount = ReadSized()
                    });
                }
                ExpectEnd();
            }

            private void ReadMembersInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    var row = new CompiledMemberRow { StartType = ReadTypeRefValue() };
                    if (row.StartType == null)
                        Malformed("A member start type is missing.");
                    int segments = ReadCount();
                    for (int s = 0; s < segments; s++)
                        row.Segments.Add(ReadStringValue());
                    int hops = ReadCount();
                    if (hops != segments)
                        Malformed("Member hops do not match the segments.");
                    for (int h = 0; h < hops; h++)
                    {
                        row.Hops.Add(new CompiledMemberHop
                        {
                            DeclaringType = ReadTypeRefValue(),
                            MemberName = ReadStringValue(),
                            MemberType = ReadTypeRefValue()
                        });
                    }
                    artifact.Members.Add(row);
                }
                ExpectEnd();
            }

            private void ReadExpressionsInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    artifact.Expressions.Add(new CompiledExpressionTree
                    {
                        Root = ReadExpression(),
                        ModelType = ReadTypeRefValue(),
                        ChainedType = ReadTypeRefValue(),
                        RootType = ReadTypeRefValue(),
                        ContainsDeferredCall = ReadBool()
                    });
                }
                ExpectEnd();
            }

            private CompiledExpression ReadExpression()
            {
                byte kind = ReadByte();
                if (kind > (byte)CompiledExprKind.MethodCall)
                    Malformed("An expression kind is out of range.");
                var node = new CompiledExpression { Kind = (CompiledExprKind)kind };
                node.Position = ReadPosition();
                switch (node.Kind)
                {
                    case CompiledExprKind.Literal:
                        node.Literal = ReadLiteralBody();
                        break;
                    case CompiledExprKind.This:
                        break;
                    case CompiledExprKind.Path:
                        node.RootRef = ReadBool();
                        int segments = ReadCount();
                        for (int s = 0; s < segments; s++)
                            node.Segments.Add(ReadStringValue());
                        if (ReadBool())
                            node.Target = ReadExpression();
                        break;
                    case CompiledExprKind.Index:
                        node.Target = ReadExpression();
                        ReadExpressionListInto(node.Arguments);
                        break;
                    case CompiledExprKind.Call:
                        node.Name = ReadStringValue();
                        ReadExpressionListInto(node.Arguments);
                        break;
                    case CompiledExprKind.Unary:
                        node.Operator = ReadOperator();
                        node.Operand = ReadExpression();
                        break;
                    case CompiledExprKind.Binary:
                        node.Operator = ReadOperator();
                        node.Left = ReadExpression();
                        node.Right = ReadExpression();
                        break;
                    case CompiledExprKind.Ternary:
                        node.Condition = ReadExpression();
                        node.WhenTrue = ReadExpression();
                        node.WhenFalse = ReadExpression();
                        break;
                    case CompiledExprKind.MethodCall:
                        node.Target = ReadExpression();
                        node.Name = ReadStringValue();
                        ReadExpressionListInto(node.Arguments);
                        break;
                }
                return node;
            }

            private void ReadExpressionListInto(IList<CompiledExpression> arguments)
            {
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                    arguments.Add(ReadExpression());
            }

            private CompiledExprOperator ReadOperator()
            {
                byte op = ReadByte();
                if (op > (byte)CompiledExprOperator.OnesComplement)
                    Malformed("An expression operator is out of range.");
                return (CompiledExprOperator)op;
            }

            private void ReadCSharpInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    var site = new CompiledCSharpSite { Source = ReadStringValue() };
                    int usings = ReadCount();
                    for (int u = 0; u < usings; u++)
                        site.Usings.Add(ReadStringValue());
                    site.ModelType = ReadTypeRefValue();
                    site.ChainedType = ReadTypeRefValue();
                    site.RootType = ReadTypeRefValue();
                    site.Position = ReadPosition();
                    artifact.CSharpSites.Add(site);
                }
                ExpectEnd();
            }

            private void ReadDocumentsInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    var document = new CompiledDocument
                    {
                        ShapedText = ReadStringValue(),
                        NeedsLocals = ReadBool(),
                        ParseFacts = ReadParseFacts()
                    };
                    int elements = ReadCount();
                    for (int e = 0; e < elements; e++)
                    {
                        var element = new CompiledElement { IsChain = ReadBool() };
                        if (element.IsChain)
                            element.Chain = ReadChain(artifact);
                        else
                            element.StaticPiece = ReadStringValue();
                        document.Elements.Add(element);
                    }
                    artifact.Documents.Add(document);
                }
                ExpectEnd();
            }

            private CompiledParseFacts ReadParseFacts()
            {
                var facts = new CompiledParseFacts { Offset = ReadSized() };
                facts.InDefinitionContext = ReadBool();
                int visible = ReadCount();
                for (int i = 0; i < visible; i++)
                    facts.VisibleDefinitionRefs.Add(ReadSized());
                return facts;
            }

            private CompiledChain ReadChain(CompiledArtifact artifact)
            {
                var chain = new CompiledChain();
                int items = ReadCount();
                for (int i = 0; i < items; i++)
                    chain.Items.Add(ReadItem(artifact));
                return chain;
            }

            private CompiledItem ReadItem(CompiledArtifact artifact)
            {
                var item = new CompiledItem
                {
                    ExtensionRef = ReadSized(),
                    Position = ReadPosition(),
                    ReturnType = ReadTypeRefValue(),
                    ParameterTemplate = ReadOptStringValue()
                };
                item.Parameter = ReadParameter(artifact);
                if (ReadBool())
                {
                    item.Body = new CompiledBody
                    {
                        RawText = ReadStringValue(),
                        ShapedText = ReadStringValue(),
                        DataType = ReadTypeRefValue(),
                        ChainedType = ReadTypeRefValue()
                    };
                    if (ReadBool())
                        item.Body.CompiledDocumentRef = ReadSized();
                }
                if (ReadBool())
                    item.Props = ReadProps();
                return item;
            }

            private CompiledParameter ReadParameter(CompiledArtifact artifact)
            {
                byte kind = ReadByte();
                if (kind > (byte)CompiledParameterKind.PropsSlot)
                    Malformed("A parameter kind is out of range.");
                var parameter = new CompiledParameter { Kind = (CompiledParameterKind)kind };
                switch (parameter.Kind)
                {
                    case CompiledParameterKind.None:
                        break;
                    case CompiledParameterKind.Constant:
                        parameter.Constant = ReadLiteralBody();
                        break;
                    case CompiledParameterKind.ModelPath:
                    case CompiledParameterKind.RootPath:
                        parameter.MemberRef = ReadSized();
                        break;
                    case CompiledParameterKind.DynamicPath:
                        int segments = ReadCount();
                        for (int s = 0; s < segments; s++)
                            parameter.Segments.Add(ReadStringValue());
                        break;
                    case CompiledParameterKind.Chain:
                        parameter.NestedChain = ReadChain(artifact);
                        break;
                    case CompiledParameterKind.NativeExpression:
                        parameter.ExpressionRef = ReadSized();
                        parameter.UsesPropsSlot = ReadBool();
                        break;
                    case CompiledParameterKind.CSharpExpression:
                        parameter.SiteRef = ReadSized();
                        break;
                    case CompiledParameterKind.LateBoundCall:
                        parameter.ExpressionRef = ReadSized();
                        break;
                    case CompiledParameterKind.RefusalSite:
                        parameter.Refusal = ReadRefusalSource();
                        break;
                    case CompiledParameterKind.DefinitionCall:
                        parameter.DefinitionRef = ReadSized();
                        parameter.CallerContentRef = ReadSized();
                        parameter.SlotMode = ReadBool();
                        break;
                    case CompiledParameterKind.PropsSlot:
                        parameter.SlotIndex = ReadSized();
                        int rest = ReadCount();
                        parameter.Segments = new List<string>(rest);
                        for (int s = 0; s < rest; s++)
                            parameter.Segments.Add(ReadStringValue());
                        int propHops = ReadCount();
                        parameter.PropHops = new List<CompiledMemberHop>(propHops);
                        for (int h = 0; h < propHops; h++)
                        {
                            parameter.PropHops.Add(new CompiledMemberHop
                            {
                                DeclaringType = ReadTypeRefValue(),
                                MemberName = ReadStringValue(),
                                MemberType = ReadTypeRefValue()
                            });
                        }

                        parameter.PropDynamicRest = ReadBool();
                        break;
                }
                return parameter;
            }

            private CompiledRefusalSource ReadRefusalSource()
            {
                var refusal = new CompiledRefusalSource { SourceText = ReadStringValue() };
                refusal.ModelType = ReadTypeRefValue();
                refusal.ChainedType = ReadTypeRefValue();
                refusal.RootType = ReadTypeRefValue();
                int namespaces = ReadCount();
                for (int i = 0; i < namespaces; i++)
                    refusal.Namespaces.Add(ReadStringValue());
                refusal.Position = ReadPosition();
                byte @class = ReadByte();
                if (@class > 2)
                    Malformed("A refusal class is out of range.");
                refusal.Class = (PrecompiledRefusalClass)@class;
                return refusal;
            }

            private CompiledProps ReadProps()
            {
                var props = new CompiledProps();
                int prototype = ReadCount();
                for (int i = 0; i < prototype; i++)
                    props.FrozenPrototype.Add(ReadOptLiteral());
                int slots = ReadCount();
                for (int i = 0; i < slots; i++)
                {
                    props.DynamicSlots.Add(new CompiledDynamicSlot
                    {
                        SlotIndex = ReadSized(),
                        ExpressionRef = ReadSized(),
                        TargetType = ReadTypeRefValue()
                    });
                }
                return props;
            }

            private void ReadDefinitionsInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    var definition = new CompiledDefinition
                    {
                        Name = ReadStringValue(),
                        BaseName = ReadOptStringValue(),
                        ModelTypeSpelling = ReadStringValue(),
                        ModelType = ReadTypeRefValue(),
                        ParameterTemplate = ReadOptStringValue()
                    };
                    int props = ReadCount();
                    for (int p = 0; p < props; p++)
                    {
                        definition.PropDecls.Add(new CompiledPropDecl
                        {
                            Name = ReadStringValue(),
                            SlotIndex = ReadSized(),
                            SlotType = ReadTypeRefValue(),
                            DefaultValue = ReadOptLiteral()
                        });
                    }
                    definition.SlotType = ReadTypeRefValue();
                    int regions = ReadCount();
                    for (int r = 0; r < regions; r++)
                        definition.Regions.Add(ReadStringValue());
                    int fills = ReadCount();
                    for (int f = 0; f < fills; f++)
                    {
                        definition.Fills.Add(new CompiledRegionFill
                        {
                            RegionName = ReadStringValue(),
                            DocumentRef = ReadSized()
                        });
                    }
                    definition.Position = ReadPosition();
                    artifact.Definitions.Add(definition);
                }
                ExpectEnd();
            }

            private void ReadTemplatesInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    var template = new CompiledTemplateRow
                    {
                        Key = ReadStringValue(),
                        RegisteredName = ReadOptStringValue(),
                        ContentHash = ReadStringValue(),
                        ModelType = ReadTypeRefValue()
                    };
                    template.ModelTypeIsAmbient = ReadBool();
                    template.IsDynamic = ReadBool();
                    template.EntryPointTypeName = ReadOptStringValue();
                    int imports = ReadCount();
                    for (int m = 0; m < imports; m++)
                    {
                        template.Imports.Add(new CompiledImport
                        {
                            Key = ReadStringValue(),
                            ContentHash = ReadStringValue()
                        });
                    }
                    template.Options = new CompiledOptionsFingerprint
                    {
                        Profile = ReadOptStringValue(),
                        Mode = ReadStringValue()
                    };
                    template.Options.Trim = ReadBool();
                    ReadIndexListInto(template.ExtensionRefs, artifact.Extensions.Count, "extension");
                    ReadIndexListInto(template.FunctionRefs, artifact.Functions.Count, "function");
                    template.RootDocumentRef = ReadIndex(artifact.Documents.Count, "document");
                    ReadIndexListInto(template.DefinitionRefs, artifact.Definitions.Count, "definition");
                    template.SiteCount = ReadSized();
                    int refusals = ReadCount();
                    for (int r = 0; r < refusals; r++)
                    {
                        int ordinal = ReadSized();
                        byte @class = ReadByte();
                        if (@class > 2)
                            Malformed("A refusal site class is out of range.");
                        template.RefusalSites.Add(new PrecompiledRefusalSite(
                            ordinal, (PrecompiledRefusalClass)@class, ReadStringValue(),
                            ReadSized(), ReadSized()));
                    }
                    artifact.Templates.Add(template);
                }
                ExpectEnd();
            }

            private void ReadIndexListInto(IList<int> refs, int count, string what)
            {
                int items = ReadCount();
                for (int i = 0; i < items; i++)
                    refs.Add(ReadIndex(count, what));
            }

            private void ReadSitesInto(CompiledArtifact artifact, byte[] blob)
            {
                Open(blob);
                int count = ReadCount();
                for (int i = 0; i < count; i++)
                {
                    var site = new CompiledSiteRow
                    {
                        TemplateIndex = ReadSized(),
                        SiteOrdinal = ReadSized()
                    };
                    if (site.TemplateIndex >= artifact.Templates.Count)
                        Malformed("A site template index is out of range.");
                    byte kind = ReadByte();
                    if (kind > (byte)CompiledSiteKind.Refusal)
                        Malformed("A site kind is out of range.");
                    site.Kind = (CompiledSiteKind)kind;
                    site.PayloadRef = ReadSized();
                    if (site.PayloadRef >= SitePayloadLimit(site.Kind, artifact))
                        Malformed("A site payload ref is out of range.");
                    artifact.Sites.Add(site);
                }
                ExpectEnd();
            }

            private static int SitePayloadLimit(CompiledSiteKind kind, CompiledArtifact artifact)
            {
                switch (kind)
                {
                    case CompiledSiteKind.MemberAccessor:
                        return artifact.Members.Count;
                    case CompiledSiteKind.NativeExpression:
                    case CompiledSiteKind.LateBoundCall:
                        return artifact.Expressions.Count;
                    case CompiledSiteKind.EmbeddedCSharp:
                        return artifact.CSharpSites.Count;
                    default:
                        return artifact.Documents.Count;
                }
            }

            private void ValidateRefs(CompiledArtifact artifact)
            {
                foreach (var document in artifact.Documents)
                {
                    foreach (int definitionRef in document.ParseFacts.VisibleDefinitionRefs)
                        CheckRef(definitionRef, artifact.Definitions.Count, "definition");
                    foreach (var element in document.Elements)
                        if (element.IsChain)
                            ValidateChain(element.Chain, artifact);
                }
                foreach (var definition in artifact.Definitions)
                    foreach (var fill in definition.Fills)
                        CheckRef(fill.DocumentRef, artifact.Documents.Count, "document");
                foreach (var slot in AllDynamicSlots(artifact))
                    CheckRef(slot.ExpressionRef, artifact.Expressions.Count, "expression");
            }

            private static IEnumerable<CompiledDynamicSlot> AllDynamicSlots(CompiledArtifact artifact)
            {
                foreach (var document in artifact.Documents)
                    foreach (var element in document.Elements)
                        if (element.IsChain)
                            foreach (var slot in ChainDynamicSlots(element.Chain))
                                yield return slot;
            }

            private static IEnumerable<CompiledDynamicSlot> ChainDynamicSlots(CompiledChain chain)
            {
                foreach (var item in chain.Items)
                {
                    if (item.Props != null)
                        foreach (var slot in item.Props.DynamicSlots)
                            yield return slot;
                    if (item.Parameter != null && item.Parameter.Kind == CompiledParameterKind.Chain &&
                        item.Parameter.NestedChain != null)
                        foreach (var slot in ChainDynamicSlots(item.Parameter.NestedChain))
                            yield return slot;
                }
            }

            private void ValidateChain(CompiledChain chain, CompiledArtifact artifact)
            {
                foreach (var item in chain.Items)
                {
                    CheckRef(item.ExtensionRef, artifact.Extensions.Count, "extension");
                    ValidateParameter(item.Parameter, artifact);
                    if (item.Body != null && item.Body.CompiledDocumentRef.HasValue)
                        CheckRef(item.Body.CompiledDocumentRef.Value, artifact.Documents.Count, "document");
                    if (item.Parameter != null && item.Parameter.Kind == CompiledParameterKind.Chain &&
                        item.Parameter.NestedChain != null)
                        ValidateChain(item.Parameter.NestedChain, artifact);
                }
            }

            private void ValidateParameter(CompiledParameter parameter, CompiledArtifact artifact)
            {
                if (parameter == null)
                    Malformed("An item parameter is missing.");
                switch (parameter.Kind)
                {
                    case CompiledParameterKind.ModelPath:
                    case CompiledParameterKind.RootPath:
                        CheckRef(parameter.MemberRef, artifact.Members.Count, "member");
                        break;
                    case CompiledParameterKind.NativeExpression:
                    case CompiledParameterKind.LateBoundCall:
                        CheckRef(parameter.ExpressionRef, artifact.Expressions.Count, "expression");
                        break;
                    case CompiledParameterKind.CSharpExpression:
                        CheckRef(parameter.SiteRef, artifact.Sites.Count, "site");
                        break;
                    case CompiledParameterKind.DefinitionCall:
                        CheckRef(parameter.DefinitionRef, artifact.Definitions.Count, "definition");
                        CheckRef(parameter.CallerContentRef, artifact.Documents.Count, "document");
                        break;
                }
            }

            private static void CheckRef(int index, int count, string what)
            {
                if (index < 0 || index >= count)
                    Malformed("A " + what + " index is out of range.");
            }

            private string VerifyDigest(byte[][] blobs)
            {
                var headerBlob = blobs[SectionIds.Header];
                if (headerBlob.Length < 32)
                    Malformed("The header section is too short for a digest.");
                var stored = new byte[32];
                Buffer.BlockCopy(headerBlob, headerBlob.Length - 32, stored, 0, 32);

                var copy = new byte[_image.Length];
                Buffer.BlockCopy(_image, 0, copy, 0, _image.Length);
                int digestOffset = _sectionOffsets[SectionIds.Header] + headerBlob.Length - 32;
                for (int i = 0; i < 32; i++)
                    copy[digestOffset + i] = 0;
                byte[] actual;
                using (var sha = SHA256.Create())
                    actual = sha.ComputeHash(copy);
                for (int i = 0; i < 32; i++)
                    if (stored[i] != actual[i])
                        Malformed("The artifact digest does not match its bytes.");
                return ContentHash.ToHex(actual);
            }
        }
    }
}
