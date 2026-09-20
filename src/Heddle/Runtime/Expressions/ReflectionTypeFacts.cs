using System;
using Heddle.Precompiled;

namespace Heddle.Runtime.Expressions
{
    /// <summary>
    /// The handful of type-system facts the binding rules ask about a <see cref="Type"/>, stated once so the
    /// null handling and the AQN spelling cannot fork between the callers.
    /// </summary>
    internal static class ReflectionTypeFacts
    {
        /// <summary>The <b>CLR</b> relation <c>target.IsAssignableFrom(source)</c>, exactly, with an unresolved
        /// side answering false rather than throwing.</summary>
        public static bool IsAssignableFrom(Type target, Type source) =>
            target != null && source != null && target.IsAssignableFrom(source);

        /// <summary>False for null/unresolved, open generics, pointers, and by-ref types.</summary>
        public static bool IsUsableAsPropType(Type type) =>
            type != null && !type.ContainsGenericParameters && !type.IsPointer && !type.IsByRef;

        /// <summary>The manifest identity string, through the shared <c>AqnFormatter</c>.</summary>
        public static string FormatAqn(Type type) => ReflectionTypeIdentity.AqnSansVersion(type);

        /// <summary>A human-readable spelling for diagnostic text; an unresolved type still has to read as
        /// something, so it reads as <c>&lt;unknown&gt;</c>.</summary>
        public static string Display(Type type) => type == null ? "<unknown>" : type.ToString();
    }
}
