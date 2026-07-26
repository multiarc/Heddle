using System;

namespace Heddle.Precompiled
{
    /// <summary>The per-request/registration diagnostic payload delivered to
    /// <see cref="PrecompiledTemplates.OnFallback"/>. <see cref="Detail"/> is the pinned per-reason
    /// format string; <see cref="DiagnosticId"/> is one of
    /// <c>HED7101</c>/<c>HED7102</c>/<c>HED7103</c>/<c>HED7104</c>.
    /// <para><b>Two carriers, exactly one populated.</b> A per-request event is about one template and
    /// carries <see cref="TemplateKey"/>; a registration-time event (<c>SchemaVersionUnsupported</c>,
    /// <c>EngineVersionIncompatible</c>, <c>RegisteredNameUnavailable</c>) is about an assembly, has no one
    /// template to name, and carries <see cref="AssemblyName"/>. Through 2.0 both meanings shared a single
    /// <c>Key</c> property and a host had to re-derive which one it held by switching on <see cref="Reason"/>;
    /// that property is removed in 2.1 rather than narrowed, so the change is a compile error at the reading site
    /// instead of a silent null.</para>
    /// <para>The mapping is not a convention: an event can only be built through <see cref="ForTemplate"/> or
    /// <see cref="ForAssembly"/>, each of which refuses a reason belonging to the other carrier and refuses a
    /// blank carrier, and the classifier behind them refuses a reason it does not know — so a reason added later
    /// cannot be raised until it has been assigned a carrier. (A <c>default</c> struct value bypasses both
    /// factories, as it does for every value type; nothing in the engine produces one.)</para></summary>
    public readonly struct PrecompiledFallbackEvent
    {
        private PrecompiledFallbackEvent(string templateKey, string assemblyName,
            PrecompiledFallbackReason reason, string detail, string diagnosticId)
        {
            TemplateKey = templateKey;
            AssemblyName = assemblyName;
            Reason = reason;
            Detail = detail;
            DiagnosticId = diagnosticId;
        }

        /// <summary>The normalized key of the template the event is about; <c>null</c> for a registration-time
        /// reason, which is about an assembly rather than a template.</summary>
        public string TemplateKey { get; }

        /// <summary>The simple name of the assembly the event is about; <c>null</c> for a per-request reason, which
        /// is about one resolved template.</summary>
        public string AssemblyName { get; }

        public PrecompiledFallbackReason Reason { get; }

        public string Detail { get; }

        public string DiagnosticId { get; }

        /// <summary>Builds a per-request event naming the template it is about.</summary>
        /// <exception cref="ArgumentException"><paramref name="templateKey"/> is blank, or
        /// <paramref name="reason"/> is a registration-time reason, which has no template to name.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a declared reason, so
        /// which carrier it belongs to has never been decided.</exception>
        public static PrecompiledFallbackEvent ForTemplate(string templateKey, PrecompiledFallbackReason reason,
            string detail, string diagnosticId)
        {
            if (string.IsNullOrWhiteSpace(templateKey))
                throw new ArgumentException("A per-request fallback event must name the template key it is about.",
                    nameof(templateKey));
            if (IsAssemblyScoped(reason))
                throw new ArgumentException(
                    $"'{reason}' is a registration-time reason and is about an assembly, not a template: use " +
                    nameof(ForAssembly) + ".", nameof(reason));
            return new PrecompiledFallbackEvent(templateKey, null, reason, detail, diagnosticId);
        }

        /// <summary>Builds a registration-time event naming the assembly it is about.</summary>
        /// <exception cref="ArgumentException"><paramref name="assemblyName"/> is blank, or
        /// <paramref name="reason"/> is a per-request reason, which is about one template.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="reason"/> is not a declared reason, so
        /// which carrier it belongs to has never been decided.</exception>
        public static PrecompiledFallbackEvent ForAssembly(string assemblyName, PrecompiledFallbackReason reason,
            string detail, string diagnosticId)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                throw new ArgumentException(
                    "A registration-time fallback event must name the assembly it is about.", nameof(assemblyName));
            if (!IsAssemblyScoped(reason))
                throw new ArgumentException(
                    $"'{reason}' is a per-request reason and is about one template, not an assembly: use " +
                    nameof(ForTemplate) + ".", nameof(reason));
            return new PrecompiledFallbackEvent(null, assemblyName, reason, detail, diagnosticId);
        }

        /// <summary>The reason→carrier mapping, as an exhaustive switch rather than a set membership test: a reason
        /// nobody has classified falls to the <c>default</c> and throws, so it cannot be raised at all. That is the
        /// code half of the carrier pin; the declaration half lives in
        /// <c>PrecompiledFallbackCarrierTests</c>, which checks this classification against the whole enum in both
        /// directions.</summary>
        private static bool IsAssemblyScoped(PrecompiledFallbackReason reason)
        {
            switch (reason)
            {
                // Registration-time: the manifest, or one of its rows, was rejected wholesale. There is no single
                // template to name — the schema/engine arms reject every template in the assembly at once, and a lost
                // registered name is a spelling that belongs to no template afterwards.
                case PrecompiledFallbackReason.SchemaVersionUnsupported:
                case PrecompiledFallbackReason.EngineVersionIncompatible:
                case PrecompiledFallbackReason.RegisteredNameUnavailable:
                    return true;

                // Per-request: a resolved entry failed the gauntlet, or a lookup missed on case alone. Each is about
                // exactly one template, and the assembly it came from is discoverable from the entry.
                case PrecompiledFallbackReason.UnsupportedFunction:
                case PrecompiledFallbackReason.OptionsMismatch:
                case PrecompiledFallbackReason.ExtensionBindingMismatch:
                case PrecompiledFallbackReason.FunctionBindingMismatch:
                case PrecompiledFallbackReason.StaleContent:
                case PrecompiledFallbackReason.StaleImport:
                case PrecompiledFallbackReason.CaseMismatch:
                    return false;

                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason,
                        "Undeclared fallback reason: a new reason must be assigned a carrier — a template key or an " +
                        "assembly name — before it can be reported.");
            }
        }
    }
}
