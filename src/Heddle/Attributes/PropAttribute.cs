using System;

namespace Heddle.Attributes
{
    /// <summary>
    /// <para>Declares one typed, named input parameter on a custom extension class (phase 8). The caller passes it
    /// by name at the call site (<c>@grid(Photos, columns: 4)</c>) — the identical call shape a definition with
    /// props accepts — and the extension reads its bound value at render time via
    /// <c>Scope.TryGetParameter</c>/<c>Scope.GetParameter</c>.</para>
    /// <para>One attribute per parameter (<c>AllowMultiple = true</c>); a subclass extension inherits its base's
    /// parameters (<c>Inherited = true</c>), mirroring definition-prop inheritance. A parameter is optional when
    /// it carries a default (<see cref="Default"/> non-null, or <see cref="Optional"/> set); otherwise it is
    /// required at every call site.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public sealed class PropAttribute : Attribute
    {
        public PropAttribute(string name, Type type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }

        public Type Type { get; }

        /// <summary>The default value bound when the caller omits this parameter. A non-null value makes the
        /// parameter optional. Limited to the CLR values an attribute argument may carry (primitives, string,
        /// enum, or typeof) — the same expressive range a definition prop's literal default allows.</summary>
        public object Default { get; set; }

        /// <summary>Marks the parameter optional with a null default — the only way to express an optional
        /// reference/nullable parameter whose default is null (a bare <c>Default = null</c> is indistinguishable
        /// from an unset default). Ignored when <see cref="Default"/> is non-null (already optional).</summary>
        public bool Optional { get; set; }
    }
}
