using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Heddle.Runtime.Parameters;
using Xunit;

namespace Heddle.Tests
{
    /// <summary>The key a compile shares one member accessor under. What it may ask a
    /// <see cref="PropertyInfo"/> for is pinned here: a runtime that serves reflection without metadata tokens
    /// — a published NativeAOT app — throws from <see cref="MemberInfo.MetadataToken"/>, and keying on it turned
    /// every member read of a dynamic-tier compile into a failed compile there.</summary>
    public class MemberPathKeyTests
    {
        private sealed class Model
        {
            public string Title { get; set; }

            public string Other { get; set; }
        }

        /// <summary>A property that answers everything but its metadata token, as NativeAOT's reflection does.</summary>
        private sealed class TokenlessProperty : PropertyInfo
        {
            private readonly PropertyInfo _inner;

            internal TokenlessProperty(PropertyInfo inner)
            {
                _inner = inner;
            }

            public override int MetadataToken =>
                throw new InvalidOperationException("There is no metadata token available for the given member.");

            public override PropertyAttributes Attributes => _inner.Attributes;
            public override bool CanRead => _inner.CanRead;
            public override bool CanWrite => _inner.CanWrite;
            public override Type PropertyType => _inner.PropertyType;
            public override Type DeclaringType => _inner.DeclaringType;
            public override string Name => _inner.Name;
            public override Type ReflectedType => _inner.ReflectedType;

            public override MethodInfo[] GetAccessors(bool nonPublic) => _inner.GetAccessors(nonPublic);
            public override MethodInfo GetGetMethod(bool nonPublic) => _inner.GetGetMethod(nonPublic);
            public override MethodInfo GetSetMethod(bool nonPublic) => _inner.GetSetMethod(nonPublic);
            public override ParameterInfo[] GetIndexParameters() => _inner.GetIndexParameters();
            public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index,
                CultureInfo culture) => _inner.GetValue(obj, invokeAttr, binder, index, culture);
            public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder,
                object[] index, CultureInfo culture) =>
                _inner.SetValue(obj, value, invokeAttr, binder, index, culture);
            public override object[] GetCustomAttributes(bool inherit) => _inner.GetCustomAttributes(inherit);
            public override object[] GetCustomAttributes(Type attributeType, bool inherit) =>
                _inner.GetCustomAttributes(attributeType, inherit);
            public override bool IsDefined(Type attributeType, bool inherit) =>
                _inner.IsDefined(attributeType, inherit);
        }

        private static List<(Type Type, PropertyInfo Property)> Hops(params PropertyInfo[] properties)
        {
            var hops = new List<(Type Type, PropertyInfo Property)>(properties.Length);
            foreach (var property in properties)
                hops.Add((typeof(Model), property));
            return hops;
        }

        /// <summary>Two keys over a hop whose metadata token is unavailable are still the same key, and a
        /// dictionary keyed by them still shares one accessor between the two sites that read the path.</summary>
        [Fact]
        public void APathWhoseMemberHasNoMetadataTokenIsStillOneKey()
        {
            var property = new TokenlessProperty(typeof(Model).GetProperty(nameof(Model.Title)));
            var first = new MemberPathKey(Hops(property));
            var second = new MemberPathKey(Hops(property));

            Assert.True(first.Equals(second), "two keys over the same hop must compare equal");
            Assert.Equal(first.GetHashCode(), second.GetHashCode());

            var accessors = new Dictionary<MemberPathKey, Func<object, object>>();
            Func<object, object> accessor = model => ((Model) model).Title;
            accessors.Add(first, accessor);
            Assert.True(accessors.TryGetValue(second, out var shared), "the second site must find the first's accessor");
            Assert.Same(accessor, shared);
            Assert.Single(accessors);
        }

        private static string EngineRoot([System.Runtime.CompilerServices.CallerFilePath] string thisFile = null)
            => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile), "..", "Heddle"));

        /// <summary>The one place the engine may read a metadata token: a fail-closed probe that runs only
        /// after the runtime has already refused to list a member's attributes, and whose throw is caught
        /// there and hides the member.</summary>
        private static readonly Dictionary<string, string> MetadataTokenReaders =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "MemberPathResolver.cs", "ProvablyCarriesNoHidden" }
            };

        /// <summary>Nothing on the compile or render path asks a member for its metadata token. A published
        /// NativeAOT app throws for it, and only CI publishes one — so the reading is gated here, where a new
        /// one names itself in a diff. A guarded reader is declared above by the method that owns it.</summary>
        [Fact]
        public void NothingOnTheCompilePathReadsAMemberMetadataToken()
        {
            var unexpected = new List<string>();
            foreach (var file in Directory.GetFiles(EngineRoot(), "*.cs", SearchOption.AllDirectories))
            {
                string inside = file.Substring(EngineRoot().Length);
                if (inside.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) ||
                    inside.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                    continue;
                string text = File.ReadAllText(file);
                for (int at = text.IndexOf("MetadataToken", StringComparison.Ordinal); at >= 0;
                    at = text.IndexOf("MetadataToken", at + 1, StringComparison.Ordinal))
                {
                    string name = Path.GetFileName(file);
                    if (MetadataTokenReaders.TryGetValue(name, out var allowed) &&
                        string.Equals(EnclosingMethod(text, at), allowed, StringComparison.Ordinal))
                        continue;
                    unexpected.Add(name + " in " + (EnclosingMethod(text, at) ?? "?"));
                }
            }

            Assert.True(unexpected.Count == 0, "MemberInfo.MetadataToken throws in a published NativeAOT app; " +
                "read the member itself instead: " + string.Join(", ", unexpected));
        }

        private static string EnclosingMethod(string text, int at)
        {
            var declarations = Regex.Matches(text.Substring(0, at),
                @"(?m)^\s+(?:private|internal|public|protected)[^=\r\n]*?(\w+)\s*\(");
            return declarations.Count == 0 ? null : declarations[declarations.Count - 1].Groups[1].Value;
        }

        /// <summary>Hops are the reflection objects they are: two properties of the same name on the same type,
        /// read through different <see cref="PropertyInfo"/> objects, are different hops — an accessor built for
        /// one of two loaded copies of an assembly cannot read from the other.</summary>
        [Fact]
        public void TwoHopsAreTheSameOnlyWhenTheyAreTheSameReflectionObject()
        {
            var title = typeof(Model).GetProperty(nameof(Model.Title));
            var other = typeof(Model).GetProperty(nameof(Model.Other));
            Assert.False(new MemberPathKey(Hops(title)).Equals(new MemberPathKey(Hops(other))));
            Assert.False(new MemberPathKey(Hops(title)).Equals(new MemberPathKey(Hops(new TokenlessProperty(title)))));
            Assert.True(new MemberPathKey(Hops(title, other)).Equals(new MemberPathKey(Hops(title, other))));
            Assert.False(new MemberPathKey(Hops(title, other)).Equals(new MemberPathKey(Hops(other, title))));
            Assert.Equal(new MemberPathKey(Hops(title, other)).GetHashCode(),
                new MemberPathKey(Hops(title, other)).GetHashCode());
        }
    }
}
