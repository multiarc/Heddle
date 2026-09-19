using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>Encodes a compiled-form artifact to bytes. Stateless and thread-safe: all mutable state
    /// lives in one write. Serializing equal models yields identical bytes.</summary>
    public static class CompiledFormWriter
    {
        /// <summary>Encodes <paramref name="artifact"/> to a complete artifact image.</summary>
        public static byte[] Write(CompiledArtifact artifact)
        {
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            return new Encoder().WriteArtifact(artifact);
        }

        /// <summary>Encodes <paramref name="artifact"/> and copies the image to
        /// <paramref name="destination"/>. The stream is left open.</summary>
        public static void Write(CompiledArtifact artifact, Stream destination)
        {
            if (artifact == null)
                throw new ArgumentNullException(nameof(artifact));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            var image = Write(artifact);
            destination.Write(image, 0, image.Length);
        }

        private sealed class Encoder
        {
            private readonly List<byte> _current = new List<byte>();
            private readonly Dictionary<string, int> _stringIndex =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private readonly List<string> _strings = new List<string>();
            private readonly TypeIdentityTable _types = new TypeIdentityTable();

            internal byte[] WriteArtifact(CompiledArtifact artifact)
            {
                Require(artifact.Header != null, "Header is required.");

                var blobs = new byte[SectionIds.RequiredCount + 1][];
                for (int id = SectionIds.First; id <= SectionIds.Last; id++)
                {
                    _current.Clear();
                    WriteSection(id, artifact);
                    blobs[id] = _current.ToArray();
                }

                WriteStringsSection(blobs);
                WriteTypesSection(blobs);

                var image = Assemble(blobs);
                var digestOffset = DigestFieldOffset(blobs);
                var hash = HashZeroed(image, digestOffset);
                Buffer.BlockCopy(hash, 0, image, digestOffset, hash.Length);
                artifact.DigestHex = ContentHash.ToHex(hash);
                return image;
            }

            private static void Require(bool condition, string message)
            {
                if (!condition)
                    throw new InvalidOperationException("Cannot encode the compiled form: " + message);
            }

            private static int RequireIndex(int index, int count, string what)
            {
                if (index < 0 || index >= count)
                    throw new InvalidOperationException(
                        "Cannot encode the compiled form: " + what + " index " + index +
                        " is out of range (0-" + (count - 1) + ").");
                return index;
            }

            private void WriteByte(byte value) => _current.Add(value);

            private void WriteBool(bool value) => _current.Add(value ? (byte)1 : (byte)0);

            private void WriteBytes(byte[] value)
            {
                for (int i = 0; i < value.Length; i++)
                    _current.Add(value[i]);
            }

            private void WriteU16LE(int value)
            {
                Require(value >= 0 && value <= 0xFFFF, "Section id out of range.");
                _current.Add((byte)(value & 0xFF));
                _current.Add((byte)((value >> 8) & 0xFF));
            }

            private void WriteU32LE(uint value)
            {
                _current.Add((byte)(value & 0xFF));
                _current.Add((byte)((value >> 8) & 0xFF));
                _current.Add((byte)((value >> 16) & 0xFF));
                _current.Add((byte)((value >> 24) & 0xFF));
            }

            private void WriteI32LE(int value) => WriteU32LE(unchecked((uint)value));

            private void WriteUVarint(ulong value)
            {
                do
                {
                    byte b = (byte)(value & 0x7F);
                    value >>= 7;
                    _current.Add(value == 0 ? b : (byte)(b | 0x80));
                }
                while (value != 0);
            }

            private void WriteCount(int value)
            {
                Require(value >= 0, "A count is negative.");
                WriteUVarint((uint)value);
            }

            private void WriteIndex(int value)
            {
                Require(value >= 0, "An index is negative.");
                WriteUVarint((uint)value);
            }

            private void WriteSVarint32(int value) =>
                WriteUVarint(unchecked((uint)((value << 1) ^ (value >> 31))));

            private void WriteSVarint64(long value) =>
                WriteUVarint(unchecked((ulong)((value << 1) ^ (value >> 63))));

            private void WriteDouble(double value) =>
                WriteU64LE(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));

            private void WriteU64LE(ulong value)
            {
                for (int i = 0; i < 8; i++)
                    _current.Add((byte)((value >> (8 * i)) & 0xFF));
            }

            private void WriteFloat(float value)
            {
                uint bits = unchecked((uint)BitConverter.ToInt32(BitConverter.GetBytes(value), 0));
                for (int i = 0; i < 4; i++)
                    _current.Add((byte)((bits >> (8 * i)) & 0xFF));
            }

            private void WriteDecimal(decimal value)
            {
                foreach (int part in decimal.GetBits(value))
                    WriteI32LE(part);
            }

            private int InternString(string value)
            {
                int index;
                if (_stringIndex.TryGetValue(value, out index))
                    return index;
                index = _strings.Count;
                _strings.Add(value);
                _stringIndex.Add(value, index);
                return index;
            }

            private void WriteString(string value)
            {
                Require(value != null, "A required string is null.");
                WriteIndex(InternString(value));
            }

            private void WriteOptString(string value)
            {
                WriteBool(value != null);
                if (value != null)
                    WriteIndex(InternString(value));
            }

            private void InternTypeTree(CompiledTypeRef typeRef)
            {
                var named = typeRef as NamedTypeRef;
                if (named != null)
                {
                    InternString(named.FullName);
                    InternString(named.AssemblySimpleName);
                    _types.Intern(typeRef);
                    return;
                }
                var generic = typeRef as GenericTypeRef;
                if (generic != null)
                {
                    InternString(generic.Definition.FullName);
                    InternString(generic.Definition.AssemblySimpleName);
                    for (int i = 0; i < generic.Arguments.Count; i++)
                    {
                        Require(generic.Arguments[i] != null, "A generic type argument is null.");
                        InternTypeTree(generic.Arguments[i]);
                    }
                    _types.Intern(typeRef);
                    return;
                }
                var array = typeRef as ArrayTypeRef;
                if (array != null)
                {
                    InternTypeTree(array.Element);
                    _types.Intern(typeRef);
                    return;
                }
                _types.Intern(typeRef);
            }

            private void WriteTypeRef(CompiledTypeRef typeRef)
            {
                WriteBool(typeRef != null);
                if (typeRef != null)
                {
                    InternTypeTree(typeRef);
                    WriteIndex(_types.Intern(typeRef));
                }
            }

            private void WritePosition(CompiledPosition position)
            {
                Require(position != null, "A position is null.");
                Require(position.Start >= 0 && position.Length >= 0, "A position is negative.");
                WriteUVarint((uint)position.Start);
                WriteUVarint((uint)position.Length);
            }

            private void WriteLiteralBody(CompiledLiteral literal)
            {
                Require(literal != null, "A literal is null.");
                WriteByte((byte)literal.Kind);
                switch (literal.Kind)
                {
                    case CompiledLiteralKind.Null:
                        break;
                    case CompiledLiteralKind.Int64:
                        WriteSVarint64(literal.Int64);
                        break;
                    case CompiledLiteralKind.UInt64:
                        WriteUVarint(literal.UInt64);
                        break;
                    case CompiledLiteralKind.Float:
                        WriteFloat(literal.Float32);
                        break;
                    case CompiledLiteralKind.Double:
                        WriteDouble(literal.Double);
                        break;
                    case CompiledLiteralKind.Decimal:
                        WriteDecimal(literal.Decimal);
                        break;
                    case CompiledLiteralKind.Boolean:
                        WriteBool(literal.Boolean);
                        break;
                    case CompiledLiteralKind.String:
                        WriteString(literal.Text);
                        break;
                    case CompiledLiteralKind.Char:
                        Require(literal.CharCode >= 0 && literal.CharCode <= 0xFFFF, "A char code is out of range.");
                        WriteUVarint((uint)literal.CharCode);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Cannot encode the compiled form: unknown literal kind.");
                }
            }

            private void WriteOptLiteral(CompiledLiteral literal)
            {
                WriteBool(literal != null);
                if (literal != null)
                    WriteLiteralBody(literal);
            }

            private void WriteSection(int id, CompiledArtifact artifact)
            {
                switch (id)
                {
                    case SectionIds.Header:
                        WriteHeader(artifact);
                        break;
                    case SectionIds.Strings:
                        break;
                    case SectionIds.Types:
                        break;
                    case SectionIds.Extensions:
                        WriteExtensions(artifact);
                        break;
                    case SectionIds.Functions:
                        WriteFunctions(artifact);
                        break;
                    case SectionIds.Members:
                        WriteMembers(artifact);
                        break;
                    case SectionIds.Expressions:
                        WriteExpressions(artifact);
                        break;
                    case SectionIds.CSharp:
                        WriteCSharpSites(artifact);
                        break;
                    case SectionIds.Documents:
                        WriteDocuments(artifact);
                        break;
                    case SectionIds.Definitions:
                        WriteDefinitions(artifact);
                        break;
                    case SectionIds.Templates:
                        WriteTemplates(artifact);
                        break;
                    case SectionIds.Sites:
                        WriteSites(artifact);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Cannot encode the compiled form: unknown section id.");
                }
            }

            private void WriteHeader(CompiledArtifact artifact)
            {
                var header = artifact.Header;
                WriteString(header.EngineVersion);
                WriteString(header.BuilderVersion);
                WriteString(header.ExpressionMode);
                WriteBool(header.TrimDirectiveLines);
                WriteOptString(header.DefaultOutputProfile);
                WriteCount(artifact.Templates.Count);
                for (int i = 0; i < 32; i++)
                    _current.Add(0);
            }

            private void WriteStringsSection(byte[][] blobs)
            {
                _current.Clear();
                WriteCount(_strings.Count);
                foreach (string value in _strings)
                {
                    WriteCount(value.Length);
                    for (int i = 0; i < value.Length; i++)
                    {
                        char c = value[i];
                        _current.Add((byte)(c & 0xFF));
                        _current.Add((byte)((c >> 8) & 0xFF));
                    }
                }
                blobs[SectionIds.Strings] = _current.ToArray();
            }

            private int TypeTableIndex(CompiledTypeRef typeRef)
            {
                int index;
                Require(_types.TryGetIndex(typeRef, out index), "A type reference was not interned.");
                return index;
            }

            private void WriteNamedBody(NamedTypeRef named)
            {
                WriteString(named.FullName);
                WriteString(named.AssemblySimpleName);
                WriteBool(named.IsFramework);
            }

            private void WriteTypesSection(byte[][] blobs)
            {
                _current.Clear();
                var ordered = _types.OrderedRefs;
                WriteCount(ordered.Count);
                foreach (CompiledTypeRef typeRef in ordered)
                {
                    var named = typeRef as NamedTypeRef;
                    if (named != null)
                    {
                        WriteByte(0);
                        WriteNamedBody(named);
                        continue;
                    }
                    var generic = typeRef as GenericTypeRef;
                    if (generic != null)
                    {
                        WriteByte(1);
                        WriteNamedBody(generic.Definition);
                        WriteCount(generic.Arguments.Count);
                        for (int i = 0; i < generic.Arguments.Count; i++)
                            WriteIndex(TypeTableIndex(generic.Arguments[i]));
                        continue;
                    }
                    var array = typeRef as ArrayTypeRef;
                    if (array != null)
                    {
                        WriteByte(2);
                        WriteIndex(TypeTableIndex(array.Element));
                        Require(array.Rank >= 1, "An array rank is less than 1.");
                        WriteUVarint((uint)array.Rank);
                        continue;
                    }
                    if (typeRef is DynamicTypeRef)
                    {
                        WriteByte(3);
                        continue;
                    }
                    throw new InvalidOperationException(
                        "Cannot encode the compiled form: unknown type reference.");
                }
                blobs[SectionIds.Types] = _current.ToArray();
            }

            private static byte[] Assemble(byte[][] blobs)
            {
                long headerSize = 4 + 4 + 4 + (long)SectionIds.RequiredCount * 10;
                long total = headerSize;
                var offsets = new long[SectionIds.RequiredCount + 1];
                for (int id = SectionIds.First; id <= SectionIds.Last; id++)
                {
                    offsets[id] = total;
                    total += blobs[id].Length;
                    Require(total <= uint.MaxValue, "The artifact exceeds the container size limit.");
                }

                var image = new byte[total];
                image[0] = (byte)'H';
                image[1] = (byte)'C';
                image[2] = (byte)'F';
                image[3] = (byte)'3';
                WriteU32To(image, 4, (uint)PrecompiledSchema.CompiledFormSchemaVersion);
                WriteU32To(image, 8, (uint)SectionIds.RequiredCount);
                int table = 12;
                for (int id = SectionIds.First; id <= SectionIds.Last; id++)
                {
                    image[table] = (byte)(id & 0xFF);
                    image[table + 1] = (byte)((id >> 8) & 0xFF);
                    WriteU32To(image, table + 2, (uint)offsets[id]);
                    WriteU32To(image, table + 6, (uint)blobs[id].Length);
                    Buffer.BlockCopy(blobs[id], 0, image, (int)offsets[id], blobs[id].Length);
                    table += 10;
                }
                return image;
            }

            private static void WriteU32To(byte[] image, int offset, uint value)
            {
                image[offset] = (byte)(value & 0xFF);
                image[offset + 1] = (byte)((value >> 8) & 0xFF);
                image[offset + 2] = (byte)((value >> 16) & 0xFF);
                image[offset + 3] = (byte)((value >> 24) & 0xFF);
            }

            private static int DigestFieldOffset(byte[][] blobs)
            {
                int offset = 0;
                offset += blobs[SectionIds.Header].Length - 32;
                return (int)(4 + 4 + 4 + (long)SectionIds.RequiredCount * 10) + offset;
            }

            private static byte[] HashZeroed(byte[] image, int digestOffset)
            {
                var copy = new byte[image.Length];
                Buffer.BlockCopy(image, 0, copy, 0, image.Length);
                for (int i = 0; i < 32; i++)
                    copy[digestOffset + i] = 0;
                using (var sha = SHA256.Create())
                    return sha.ComputeHash(copy);
            }

            private void WriteExtensions(CompiledArtifact artifact)
            {
                WriteCount(artifact.Extensions.Count);
                foreach (var row in artifact.Extensions)
                {
                    Require(row != null, "An extension row is null.");
                    WriteString(row.RegistryName);
                    Require(row.Type != null, "An extension type is null.");
                    WriteTypeRef(row.Type);
                    WriteOptString(row.Fingerprint);
                }
            }

            private void WriteFunctions(CompiledArtifact artifact)
            {
                WriteCount(artifact.Functions.Count);
                foreach (var row in artifact.Functions)
                {
                    Require(row != null, "A function row is null.");
                    WriteString(row.Name);
                    WriteTypeRef(row.Target);
                    Require(row.OverloadCount >= 0, "A function overload count is negative.");
                    WriteUVarint((uint)row.OverloadCount);
                }
            }

            private void WriteMembers(CompiledArtifact artifact)
            {
                WriteCount(artifact.Members.Count);
                foreach (var row in artifact.Members)
                {
                    Require(row != null, "A member row is null.");
                    Require(row.StartType != null, "A member start type is null.");
                    Require(row.Segments != null, "Member segments are null.");
                    Require(row.Hops != null, "Member hops are null.");
                    Require(row.Hops.Count == row.Segments.Count, "Member hops do not match the segments.");
                    WriteTypeRef(row.StartType);
                    WriteCount(row.Segments.Count);
                    foreach (string segment in row.Segments)
                        WriteString(segment);
                    WriteCount(row.Hops.Count);
                    foreach (var hop in row.Hops)
                    {
                        Require(hop != null, "A member hop is null.");
                        WriteTypeRef(hop.DeclaringType);
                        WriteString(hop.MemberName);
                        WriteTypeRef(hop.MemberType);
                    }
                }
            }

            private void WriteExpressions(CompiledArtifact artifact)
            {
                WriteCount(artifact.Expressions.Count);
                foreach (var tree in artifact.Expressions)
                {
                    Require(tree != null, "An expression tree is null.");
                    Require(tree.Root != null, "An expression root is null.");
                    WriteExpression(tree.Root);
                    WriteTypeRef(tree.ModelType);
                    WriteTypeRef(tree.ChainedType);
                    WriteTypeRef(tree.RootType);
                    WriteBool(tree.ContainsDeferredCall);
                }
            }

            private int _depth;

            /// <summary>The writer refuses the nesting the reader would refuse, so every artifact the
            /// build produced loads.</summary>
            private void Enter()
            {
                Require(++_depth <= CompiledFormLimits.MaxNesting,
                    "nesting exceeds " + CompiledFormLimits.MaxNesting + " levels.");
            }

            private void Leave()
            {
                _depth--;
            }

            private void WriteExpression(CompiledExpression node)
            {
                Enter();
                WriteExpressionCore(node);
                Leave();
            }

            private void WriteExpressionCore(CompiledExpression node)
            {
                Require(node != null, "An expression node is null.");
                Require(node.Kind >= CompiledExprKind.Literal && node.Kind <= CompiledExprKind.MethodCall,
                    "An expression kind is out of range.");
                WriteByte((byte)node.Kind);
                WritePosition(node.Position);
                switch (node.Kind)
                {
                    case CompiledExprKind.Literal:
                        Require(node.Literal != null, "A literal node has no value.");
                        WriteLiteralBody(node.Literal);
                        break;
                    case CompiledExprKind.This:
                        break;
                    case CompiledExprKind.Path:
                        WriteBool(node.RootRef);
                        Require(node.Segments != null, "A path node has no segments.");
                        WriteCount(node.Segments.Count);
                        foreach (string segment in node.Segments)
                            WriteString(segment);
                        WriteBool(node.Target != null);
                        if (node.Target != null)
                            WriteExpression(node.Target);
                        break;
                    case CompiledExprKind.Index:
                        Require(node.Target != null, "An index node has no target.");
                        WriteExpression(node.Target);
                        WriteExpressionList(node.Arguments);
                        break;
                    case CompiledExprKind.Call:
                        WriteString(node.Name);
                        WriteExpressionList(node.Arguments);
                        break;
                    case CompiledExprKind.Unary:
                        WriteOperator(node.Operator);
                        Require(node.Operand != null, "A unary node has no operand.");
                        WriteExpression(node.Operand);
                        break;
                    case CompiledExprKind.Binary:
                        WriteOperator(node.Operator);
                        Require(node.Left != null && node.Right != null, "A binary node is missing a side.");
                        WriteExpression(node.Left);
                        WriteExpression(node.Right);
                        break;
                    case CompiledExprKind.Ternary:
                        Require(node.Condition != null && node.WhenTrue != null && node.WhenFalse != null,
                            "A ternary node is missing a branch.");
                        WriteExpression(node.Condition);
                        WriteExpression(node.WhenTrue);
                        WriteExpression(node.WhenFalse);
                        break;
                    case CompiledExprKind.MethodCall:
                        Require(node.Target != null, "A method call node has no target.");
                        WriteExpression(node.Target);
                        WriteString(node.Name);
                        WriteExpressionList(node.Arguments);
                        break;
                }
            }

            private void WriteExpressionList(IList<CompiledExpression> arguments)
            {
                Require(arguments != null, "An argument list is null.");
                WriteCount(arguments.Count);
                foreach (var argument in arguments)
                    WriteExpression(argument);
            }

            private void WriteOperator(CompiledExprOperator op)
            {
                Require(op >= CompiledExprOperator.Add && op <= CompiledExprOperator.OnesComplement,
                    "An expression operator is out of range.");
                WriteByte((byte)op);
            }

            private void WriteCSharpSites(CompiledArtifact artifact)
            {
                WriteCount(artifact.CSharpSites.Count);
                foreach (var site in artifact.CSharpSites)
                {
                    Require(site != null, "A C# site is null.");
                    WriteString(site.Source);
                    Require(site.Usings != null, "C# usings are null.");
                    WriteCount(site.Usings.Count);
                    foreach (string use in site.Usings)
                        WriteString(use);
                    WriteTypeRef(site.ModelType);
                    WriteTypeRef(site.ChainedType);
                    WriteTypeRef(site.RootType);
                    WritePosition(site.Position);
                }
            }

            private void WriteDocuments(CompiledArtifact artifact)
            {
                WriteCount(artifact.Documents.Count);
                foreach (var document in artifact.Documents)
                {
                    Require(document != null, "A document is null.");
                    WriteString(document.ShapedText);
                    WriteString(document.RawText);
                    WriteBool(document.NeedsLocals);
                    WriteParseFacts(document.ParseFacts, artifact);
                    Require(document.Elements != null, "A document element list is null.");
                    WriteCount(document.Elements.Count);
                    foreach (var element in document.Elements)
                    {
                        Require(element != null, "A document element is null.");
                        WriteBool(element.IsChain);
                        if (element.IsChain)
                        {
                            Require(element.Chain != null, "A chain element has no chain.");
                            WriteChain(element.Chain, artifact);
                        }
                        else
                        {
                            WriteString(element.StaticPiece);
                        }
                    }
                    Require(document.RemovedItems != null, "A document removed-item list is null.");
                    WriteCount(document.RemovedItems.Count);
                    foreach (var removed in document.RemovedItems)
                        WriteRemovedItem(removed, artifact);
                }
            }

            /// <summary>Minimal removed-item encoding: the serve key and the body only. The full
            /// <see cref="WriteItem"/> shape would demand extension and parameter table rows the
            /// loader never reads for an item outside the elements.</summary>
            private void WriteRemovedItem(CompiledItem item, CompiledArtifact artifact)
            {
                Require(item != null, "A removed item is null.");
                WritePosition(item.Position);
                WriteOptString(item.ParameterTemplate);
                WriteOptBody(item.Body, artifact);
                WriteAltBodies(item.AltBodies, artifact);
            }

            private void WriteParseFacts(CompiledParseFacts facts, CompiledArtifact artifact)
            {
                Require(facts != null, "Document parse facts are null.");
                Require(facts.Offset >= 0, "A parse-facts offset is negative.");
                WriteUVarint((uint)facts.Offset);
                WriteBool(facts.InDefinitionContext);
                Require(facts.VisibleDefinitionRefs != null, "Visible definition refs are null.");
                WriteCount(facts.VisibleDefinitionRefs.Count);
                foreach (int definitionRef in facts.VisibleDefinitionRefs)
                {
                    RequireIndex(definitionRef, artifact.Definitions.Count, "Definition");
                    WriteIndex(definitionRef);
                }
            }

            private void WriteChain(CompiledChain chain, CompiledArtifact artifact)
            {
                Enter();
                WriteChainCore(chain, artifact);
                Leave();
            }

            private void WriteChainCore(CompiledChain chain, CompiledArtifact artifact)
            {
                Require(chain.Items != null, "A chain item list is null.");
                WriteCount(chain.Items.Count);
                foreach (var item in chain.Items)
                    WriteItem(item, artifact);
            }

            private void WriteItem(CompiledItem item, CompiledArtifact artifact)
            {
                Require(item != null, "A chain item is null.");
                RequireIndex(item.ExtensionRef, artifact.Extensions.Count, "Extension");
                WriteIndex(item.ExtensionRef);
                WritePosition(item.Position);
                WriteTypeRef(item.ReturnType);
                WriteOptString(item.ParameterTemplate);
                WriteParameter(item.Parameter, artifact);
                WriteOptBody(item.Body, artifact);
                WriteAltBodies(item.AltBodies, artifact);
                WriteBool(item.Props != null);
                if (item.Props != null)
                    WriteProps(item.Props, artifact);
            }

            private void WriteAltBodies(IList<CompiledAltBody> alts, CompiledArtifact artifact)
            {
                if (alts == null)
                {
                    WriteCount(0);
                    return;
                }
                WriteCount(alts.Count);
                foreach (var alt in alts)
                {
                    Require(alt != null, "An alternate body is null.");
                    WriteOptString(alt.Template);
                    WriteOptBody(alt.Body, artifact);
                }
            }

            private void WriteParameter(CompiledParameter parameter, CompiledArtifact artifact)
            {
                Require(parameter != null, "An item parameter is null.");
                Require(parameter.Kind >= CompiledParameterKind.None &&
                    parameter.Kind <= CompiledParameterKind.PropsSlot, "A parameter kind is out of range.");
                WriteByte((byte)parameter.Kind);
                switch (parameter.Kind)
                {
                    case CompiledParameterKind.None:
                        break;
                    case CompiledParameterKind.Constant:
                        Require(parameter.Constant != null, "A constant parameter has no value.");
                        WriteLiteralBody(parameter.Constant);
                        break;
                    case CompiledParameterKind.ModelPath:
                    case CompiledParameterKind.RootPath:
                        RequireIndex(parameter.MemberRef, artifact.Members.Count, "Member");
                        WriteIndex(parameter.MemberRef);
                        break;
                    case CompiledParameterKind.DynamicPath:
                        Require(parameter.Segments != null, "A dynamic path has no segments.");
                        WriteCount(parameter.Segments.Count);
                        foreach (string segment in parameter.Segments)
                            WriteString(segment);
                        break;
                    case CompiledParameterKind.Chain:
                        Require(parameter.NestedChain != null, "A chain parameter has no chain.");
                        WriteChain(parameter.NestedChain, artifact);
                        break;
                    case CompiledParameterKind.NativeExpression:
                        RequireIndex(parameter.ExpressionRef, artifact.Expressions.Count, "Expression");
                        WriteIndex(parameter.ExpressionRef);
                        WriteBool(parameter.UsesPropsSlot);
                        break;
                    case CompiledParameterKind.CSharpExpression:
                        RequireIndex(parameter.SiteRef, artifact.Sites.Count, "Site");
                        WriteIndex(parameter.SiteRef);
                        break;
                    case CompiledParameterKind.LateBoundCall:
                        RequireIndex(parameter.ExpressionRef, artifact.Expressions.Count, "Expression");
                        WriteIndex(parameter.ExpressionRef);
                        break;
                    case CompiledParameterKind.RefusalSite:
                        WriteRefusalSource(parameter.Refusal);
                        break;
                    case CompiledParameterKind.DefinitionCall:
                        RequireIndex(parameter.DefinitionRef, artifact.Definitions.Count, "Definition");
                        WriteIndex(parameter.DefinitionRef);
                        RequireIndex(parameter.CallerContentRef, artifact.Documents.Count, "Document");
                        WriteIndex(parameter.CallerContentRef);
                        WriteBool(parameter.SlotMode);
                        break;
                    case CompiledParameterKind.PropsSlot:
                        Require(parameter.SlotIndex >= 0, "A prop slot index is negative.");
                        WriteUVarint((uint)parameter.SlotIndex);
                        Require(parameter.Segments != null, "A prop rest path is null.");
                        WriteCount(parameter.Segments.Count);
                        foreach (string segment in parameter.Segments)
                            WriteString(segment);
                        Require(parameter.PropHops != null, "Prop hops are null.");
                        WriteCount(parameter.PropHops.Count);
                        foreach (var hop in parameter.PropHops)
                        {
                            Require(hop != null, "A prop hop is null.");
                            WriteTypeRef(hop.DeclaringType);
                            WriteString(hop.MemberName);
                            WriteTypeRef(hop.MemberType);
                        }

                        WriteBool(parameter.PropDynamicRest);
                        break;
                }
            }

            private void WriteRefusalSource(CompiledRefusalSource refusal)
            {
                Require(refusal != null, "A refusal parameter has no source.");
                WriteString(refusal.SourceText);
                WriteTypeRef(refusal.ModelType);
                WriteTypeRef(refusal.ChainedType);
                WriteTypeRef(refusal.RootType);
                Require(refusal.Namespaces != null, "Refusal namespaces are null.");
                WriteCount(refusal.Namespaces.Count);
                foreach (string ns in refusal.Namespaces)
                    WriteString(ns);
                WritePosition(refusal.Position);
                Require((int)refusal.Class >= 0 && (int)refusal.Class <= 2, "A refusal class is out of range.");
                WriteByte((byte)refusal.Class);
            }

            private void WriteOptBody(CompiledBody body, CompiledArtifact artifact)
            {
                WriteBool(body != null);
                if (body == null)
                    return;
                WriteString(body.RawText);
                WriteString(body.ShapedText);
                WriteTypeRef(body.DataType);
                WriteTypeRef(body.ChainedType);
                WriteBool(body.CompiledDocumentRef.HasValue);
                if (body.CompiledDocumentRef.HasValue)
                {
                    RequireIndex(body.CompiledDocumentRef.Value, artifact.Documents.Count, "Document");
                    WriteUVarint((uint)body.CompiledDocumentRef.Value);
                }
            }

            private void WriteProps(CompiledProps props, CompiledArtifact artifact)
            {
                Require(props.FrozenPrototype != null, "A frozen prototype is null.");
                WriteCount(props.FrozenPrototype.Count);
                foreach (var entry in props.FrozenPrototype)
                    WriteOptLiteral(entry);
                Require(props.DynamicSlots != null, "Dynamic slots are null.");
                WriteCount(props.DynamicSlots.Count);
                foreach (var slot in props.DynamicSlots)
                {
                    Require(slot != null, "A dynamic slot is null.");
                    Require(slot.SlotIndex >= 0, "A dynamic slot index is negative.");
                    WriteUVarint((uint)slot.SlotIndex);
                    RequireIndex(slot.ExpressionRef, artifact.Expressions.Count, "Expression");
                    WriteUVarint((uint)slot.ExpressionRef);
                    WriteTypeRef(slot.TargetType);
                }
            }

            private void WriteDefinitions(CompiledArtifact artifact)
            {
                WriteCount(artifact.Definitions.Count);
                foreach (var definition in artifact.Definitions)
                {
                    Require(definition != null, "A definition is null.");
                    WriteString(definition.Name);
                    WriteOptString(definition.BaseName);
                    WriteString(definition.ModelTypeSpelling);
                    WriteTypeRef(definition.ModelType);
                    WriteOptString(definition.ParameterTemplate);
                    Require(definition.PropDecls != null, "Prop declarations are null.");
                    WriteCount(definition.PropDecls.Count);
                    foreach (var prop in definition.PropDecls)
                    {
                        Require(prop != null, "A prop declaration is null.");
                        WriteString(prop.Name);
                        Require(prop.SlotIndex >= 0, "A prop slot index is negative.");
                        WriteUVarint((uint)prop.SlotIndex);
                        WriteTypeRef(prop.SlotType);
                        WriteOptLiteral(prop.DefaultValue);
                    }
                    WriteTypeRef(definition.SlotType);
                    Require(definition.Regions != null, "Region declarations are null.");
                    WriteCount(definition.Regions.Count);
                    foreach (string region in definition.Regions)
                        WriteString(region);
                    Require(definition.Fills != null, "Region fills are null.");
                    WriteCount(definition.Fills.Count);
                    foreach (var fill in definition.Fills)
                    {
                        Require(fill != null, "A region fill is null.");
                        WriteString(fill.RegionName);
                        RequireIndex(fill.DocumentRef, artifact.Documents.Count, "Document");
                        WriteIndex(fill.DocumentRef);
                    }
                    WritePosition(definition.Position);
                }
            }

            private void WriteTemplates(CompiledArtifact artifact)
            {
                WriteCount(artifact.Templates.Count);
                foreach (var template in artifact.Templates)
                {
                    Require(template != null, "A template row is null.");
                    WriteString(template.Key);
                    WriteOptString(template.RegisteredName);
                    WriteString(template.ContentHash);
                    WriteTypeRef(template.ModelType);
                    WriteBool(template.ModelTypeIsAmbient);
                    WriteBool(template.IsDynamic);
                    WriteOptString(template.EntryPointTypeName);
                    WriteBool(template.IsImportOnly);
                    Require(template.Imports != null, "Template imports are null.");
                    WriteCount(template.Imports.Count);
                    foreach (var import in template.Imports)
                    {
                        Require(import != null, "A template import is null.");
                        WriteString(import.Key);
                        WriteString(import.ContentHash);
                    }
                    Require(template.Options != null, "A template options fingerprint is null.");
                    WriteOptString(template.Options.Profile);
                    WriteString(template.Options.Mode);
                    WriteBool(template.Options.Trim);
                    WriteIndexList(template.ExtensionRefs, artifact.Extensions.Count, "Extension");
                    WriteIndexList(template.FunctionRefs, artifact.Functions.Count, "Function");
                    RequireIndex(template.RootDocumentRef, artifact.Documents.Count, "Document");
                    WriteIndex(template.RootDocumentRef);
                    WriteIndexList(template.DefinitionRefs, artifact.Definitions.Count, "Definition");
                    Require(template.SiteCount >= 0, "A template site count is negative.");
                    WriteUVarint((uint)template.SiteCount);
                    Require(template.RefusalSites != null, "Template refusal sites are null.");
                    WriteCount(template.RefusalSites.Count);
                    foreach (var refusal in template.RefusalSites)
                    {
                        Require(refusal.SiteOrdinal >= 0, "A refusal site ordinal is negative.");
                        WriteUVarint((uint)refusal.SiteOrdinal);
                        Require((int)refusal.Class >= 0 && (int)refusal.Class <= 2,
                            "A refusal site class is out of range.");
                        WriteByte((byte)refusal.Class);
                        Require(refusal.Detail != null, "A refusal site detail is null.");
                        WriteString(refusal.Detail);
                        Require(refusal.PositionStart >= 0 && refusal.PositionLength >= 0,
                            "A refusal site position is negative.");
                        WriteUVarint((uint)refusal.PositionStart);
                        WriteUVarint((uint)refusal.PositionLength);
                    }
                }
            }

            private void WriteIndexList(IList<int> refs, int count, string what)
            {
                Require(refs != null, "A reference list is null.");
                WriteCount(refs.Count);
                foreach (int index in refs)
                {
                    RequireIndex(index, count, what);
                    WriteIndex(index);
                }
            }

            private void WriteSites(CompiledArtifact artifact)
            {
                WriteCount(artifact.Sites.Count);
                foreach (var site in artifact.Sites)
                {
                    Require(site != null, "A site row is null.");
                    Require(site.TemplateIndex >= 0 && site.TemplateIndex < artifact.Templates.Count,
                        "A site template index is out of range.");
                    WriteUVarint((uint)site.TemplateIndex);
                    Require(site.SiteOrdinal >= 0, "A site ordinal is negative.");
                    WriteUVarint((uint)site.SiteOrdinal);
                    Require(site.Kind >= CompiledSiteKind.MemberAccessor && site.Kind <= CompiledSiteKind.Refusal,
                        "A site kind is out of range.");
                    WriteByte((byte)site.Kind);
                    Require(site.PayloadRef >= 0, "A site payload ref is negative.");
                    RequireSitePayload(site, artifact);
                    WriteUVarint((uint)site.PayloadRef);
                }
            }

            private static void RequireSitePayload(CompiledSiteRow site, CompiledArtifact artifact)
            {
                int limit;
                switch (site.Kind)
                {
                    case CompiledSiteKind.MemberAccessor:
                        limit = artifact.Members.Count;
                        break;
                    case CompiledSiteKind.NativeExpression:
                    case CompiledSiteKind.LateBoundCall:
                        limit = artifact.Expressions.Count;
                        break;
                    case CompiledSiteKind.EmbeddedCSharp:
                        limit = artifact.CSharpSites.Count;
                        break;
                    case CompiledSiteKind.Refusal:
                        // The payload indexes the owning row's RefusalSites list.
                        RequireIndex(site.TemplateIndex, artifact.Templates.Count, "Site template index");
                        limit = artifact.Templates[site.TemplateIndex].RefusalSites.Count;
                        break;
                    default:
                        limit = artifact.Documents.Count;
                        break;
                }
                RequireIndex(site.PayloadRef, limit, "Site payload");
            }
        }
    }
}
