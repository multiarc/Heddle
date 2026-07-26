using System;
using Heddle.Runtime.Expressions;

namespace Heddle.Data {
    public class TemplateOptions: IEquatable<TemplateOptions>
    {
        public string FileNamePostfix { get; set; }
        public string RootPath { get; set; }
        public string TemplateName { get; }
        public bool EnableFileChangeCheck { get; set; }

        /// <summary>Expression tier for this template and its child compiles. Default: <see cref="Data.ExpressionMode.Native"/>.</summary>
        public ExpressionMode ExpressionMode { get; set; }

        /// <summary>Registered functions callable from native expressions. Null means
        /// <see cref="FunctionRegistry.Default"/>. The registry freezes on first compile use.</summary>
        public FunctionRegistry Functions { get; set; }

        /// <summary>Obsolete bridge to <see cref="ExpressionMode"/>: <c>true</c> == <see cref="Data.ExpressionMode.FullCSharp"/>.</summary>
        [Obsolete("Use ExpressionMode. AllowCSharp == true is equivalent to ExpressionMode.FullCSharp; false selects Native (or leaves MemberPathsOnly untouched).")]
        public bool AllowCSharp
        {
            get => ExpressionMode == ExpressionMode.FullCSharp;
            set
            {
                if (value)
                    ExpressionMode = ExpressionMode.FullCSharp;
                else if (ExpressionMode == ExpressionMode.FullCSharp)
                    ExpressionMode = ExpressionMode.Native;
            }
        }

        public int MaxRecursionCount { get; set; }
        public bool ProvideLanguageFeatures { get; set; }
        public object Data { get; set; }

        /// <summary>Governs handling when a precompiled entry fails validation: <see cref="PrecompiledMismatchPolicy.Fallback"/>
        /// (default) recompiles; <see cref="PrecompiledMismatchPolicy.Strict"/> throws. Absent from
        /// <see cref="Equals(TemplateOptions)"/> — changes handling, not output.</summary>
        public PrecompiledMismatchPolicy PrecompiledMismatchPolicy { get; set; }

        /// <summary>Output profile for this template and its child compiles. Default:
        /// <see cref="Data.OutputProfile.Html"/>. Participates in cache key.</summary>
        public OutputProfile OutputProfile { get; set; }

        /// <summary>When <c>true</c>, whole-line directives swallow their line terminator. Default: <c>true</c>.
        /// Participates in cache key; compile-time only.</summary>
        public bool TrimDirectiveLines { get; set; }

        /// <summary>Output encoder at HTML-encoding sites. <c>null</c> selects the legacy built-in path; supply a
        /// <see cref="System.Text.Encodings.Web.TextEncoder"/> for modern contract. Participates in cache key
        /// by reference; thread-safe implementations are required.</summary>
        public System.Text.Encodings.Web.TextEncoder Encoder { get; set; }

        /// <summary>Per-render resource limits (output, ops, time). <c>null</c> (default) is unlimited with zero cost.
        /// Absent from cache keys — does not affect compiled structure or bytes of successful renders.</summary>
        public RenderBudget RenderBudget { get; set; }

        /// <summary>When <c>true</c>, validates data against the template's compiled model type; throws
        /// <see cref="Heddle.Exceptions.TemplateProcessingException"/> on mismatch. Default: <c>false</c>.
        /// Absent from cache keys — changes failure handling, not successful output.</summary>
        public bool ValidateModelType { get; set; }

        public TemplateOptions() : this((string) null)
        {
        }

        /// <summary>Defaults come from <see cref="Heddle.Precompiled.HeddleBuildOptions"/>, shared with the generator and MSBuild.</summary>
        public TemplateOptions(string templateName) {
            FileNamePostfix = string.Empty;
            RootPath = AppContext.BaseDirectory;
            TemplateName = templateName ?? string.Empty;
            EnableFileChangeCheck = false;
            ExpressionMode = Heddle.Precompiled.HeddleBuildOptions.DefaultExpressionMode;
            MaxRecursionCount = Heddle.Precompiled.HeddleBuildOptions.DefaultMaxRecursionCount;
            OutputProfile = Heddle.Precompiled.HeddleBuildOptions.DefaultOutputProfile;
            TrimDirectiveLines = Heddle.Precompiled.HeddleBuildOptions.DefaultTrimDirectiveLines;
        }

        public TemplateOptions(TemplateOptions value, string templateName = null)
        {
            FileNamePostfix = value.FileNamePostfix;
            RootPath = value.RootPath ?? throw new ArgumentException();
            TemplateName = templateName ?? value.TemplateName;
            EnableFileChangeCheck = value.EnableFileChangeCheck;
            ExpressionMode = value.ExpressionMode;
            Functions = value.Functions;
            MaxRecursionCount = value.MaxRecursionCount;
            ProvideLanguageFeatures = value.ProvideLanguageFeatures;
            Data = value.Data;
            OutputProfile = value.OutputProfile;
            TrimDirectiveLines = value.TrimDirectiveLines;
            PrecompiledMismatchPolicy = value.PrecompiledMismatchPolicy;
            Encoder = value.Encoder;
            RenderBudget = value.RenderBudget;   // Copied, but not part of Equals/GetHashCode or the fingerprint.
            ValidateModelType = value.ValidateModelType;   // Copied, but not part of Equals/GetHashCode or the fingerprint.
        }

        /// <summary>The composed on-disk path, using <see cref="System.IO.Path.Combine(string,string)"/>.</summary>
        public string FullPath =>
            System.IO.Path.Combine(RootPath ?? string.Empty, TemplateName + FileNamePostfix);

        public bool Equals(TemplateOptions other)
        {
            return other.FileNamePostfix == FileNamePostfix && other.TemplateName == TemplateName && other.RootPath == RootPath && other.OutputProfile == OutputProfile && other.TrimDirectiveLines == TrimDirectiveLines && ReferenceEquals(other.Encoder, Encoder);
        }

        public static bool operator == (TemplateOptions value1, TemplateOptions value2)
        {
            return Equals(value1, value2);
        }

        public static bool operator != (TemplateOptions value1, TemplateOptions value2)
        {
            return !Equals(value1, value2);
        }

        public override bool Equals (object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (!(obj is TemplateOptions))
                return false;
            return Equals((TemplateOptions) obj);
        }

        public override int GetHashCode ()
        {
            unchecked {
                var hash = ((((TemplateName?.GetHashCode() ?? 0) * 397) ^ (int) OutputProfile) * 397) ^ (TrimDirectiveLines ? 1 : 0);
                // By-reference identity: TextEncoder does not override GetHashCode, so its default is reference-based
                // — a different encoder instance yields a different key.
                return (hash * 397) ^ (Encoder != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Encoder) : 0);
            }
        }
    }
}