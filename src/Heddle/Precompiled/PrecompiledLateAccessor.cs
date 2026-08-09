using System;

namespace Heddle.Precompiled
{
    /// <summary>
    /// One member read inside a body the build emitted <b>type-agnostically</b> — a body whose model type the build
    /// could not resolve, because the extension hosting it decides that type in its own compile-time hook.
    /// <para>The build writes the path and nothing else; the delegate behind it is built by
    /// <see cref="PrecompiledRuntime.Init"/> the moment the hook has answered, over the engine's own member walk
    /// (<see cref="PrecompiledRuntime.MemberAccessor"/>/<see cref="PrecompiledRuntime.NativeAccessor"/>), so the value
    /// is the engine's by construction rather than by the build having guessed the type right. Constructing the
    /// accessor <i>object</i> before the site and filling it afterwards is what keeps the generated file free of any
    /// static-field ordering requirement: a field initializer may name this object before the initializer that binds
    /// it has run.</para>
    /// <para><b>Render cost is one field read and one delegate call</b>, the same as a directly emitted accessor
    /// field; nothing here allocates per render. A path the engine's walk does not resolve leaves the delegate
    /// unbound and records a template-scope fault on the site — the engine refuses that read at compile time too, so
    /// the whole template belongs on the dynamic tier rather than this one read failing at render.</para>
    /// </summary>
    public sealed class PrecompiledLateAccessor
    {
        private static readonly Func<object, object, object, object> Unbound = (m, c, r) => null;

        private Func<object, object, object, object> _read = Unbound;

        /// <summary>Declares one member read of a type-agnostic body.</summary>
        /// <param name="segments">The member path, one property name per hop.</param>
        /// <param name="rootRef">Whether the path is <c>::</c>-rooted, which reads the root model rather than the
        /// body's own.</param>
        /// <exception cref="ArgumentNullException"><paramref name="segments"/> is null.</exception>
        public PrecompiledLateAccessor(string[] segments, bool rootRef = false)
        {
            Segments = segments ?? throw new ArgumentNullException(nameof(segments));
            RootReference = rootRef;
        }

        /// <summary>The member path this accessor reads.</summary>
        public string[] Segments { get; }

        /// <summary>Whether the path is <c>::</c>-rooted.</summary>
        public bool RootReference { get; }

        /// <summary>Whether <see cref="PrecompiledRuntime.Init"/> managed to build the engine's accessor for this
        /// path. False leaves the read answering <c>null</c>, and the site carries the fault that takes the template
        /// off this tier before any render reaches here.</summary>
        public bool IsBound { get; private set; }

        /// <summary>Reads the path over the engine's own accessor. One delegate call; no allocation beyond the box
        /// the engine's own accessor produces for a value-type result.</summary>
        public object Read(object model, object chained, object root) => _read(model, chained, root);

        /// <summary>Binds the engine's accessor. Called once, from <see cref="PrecompiledRuntime.Init"/>, before
        /// anything can render. A <c>::</c>-rooted path walks from <paramref name="rootType"/>, which the site
        /// already knows, and every other path from the model type the hook just answered with.</summary>
        /// <returns>The failure message, or <c>null</c> on success.</returns>
        internal string Bind(Type modelType, Type rootType)
        {
            var startType = RootReference ? rootType : modelType;
            if (startType == null)
                return "the hook answered no model type for this body, so '" + string.Join(".", Segments) +
                       "' has nothing to resolve against";
            try
            {
                _read = PrecompiledRuntime.NativeAccessor(startType, Segments, RootReference);
                IsBound = true;
                return null;
            }
            catch (Exception e)
            {
                return "'" + string.Join(".", Segments) + "' does not resolve on '" + startType.FullName + "': " +
                       e.Message;
            }
        }
    }
}
