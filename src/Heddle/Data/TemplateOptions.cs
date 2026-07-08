using Microsoft.DotNet.PlatformAbstractions;
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

        /// <summary>
        /// <para>Bridge over <see cref="ExpressionMode"/>: <c>true</c> == <see cref="Data.ExpressionMode.FullCSharp"/>.
        /// Setting <c>false</c> leaves <see cref="Data.ExpressionMode.MemberPathsOnly"/> untouched and otherwise
        /// selects <see cref="Data.ExpressionMode.Native"/>.</para>
        /// <para>Scheduled for <c>[Obsolete]</c> in the 2.0 window.</para>
        /// </summary>
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

        /// <summary>
        /// <para>Governs handling when a precompiled template entry exists for a lookup but fails the
        /// per-request validation gauntlet (phase 7 D8). <see cref="PrecompiledMismatchPolicy.Fallback"/>
        /// (default) recompiles dynamically and raises <c>HED7101</c>; <see cref="PrecompiledMismatchPolicy.Strict"/>
        /// throws. A registry miss is unaffected by this setting.</para>
        /// <para>Copied by the copy constructor; deliberately absent from <see cref="Equals(TemplateOptions)"/>/
        /// <see cref="GetHashCode"/> and the resolver cache key — it changes failure handling, never output
        /// bytes.</para>
        /// </summary>
        public PrecompiledMismatchPolicy PrecompiledMismatchPolicy { get; set; }

        /// <summary>
        /// <para>Output profile for this template and its child compiles (bodies, partials, imports).
        /// Default: <see cref="Data.OutputProfile.Html"/> (2.0) — the unnamed <c>@(...)</c> encodes by
        /// default; opt out per output with <c>@raw</c> or per template with <see cref="Data.OutputProfile.Text"/>.</para>
        /// <para>Participates in <see cref="Equals(TemplateOptions)"/>/<see cref="GetHashCode"/> — the
        /// profile keys template caches.</para>
        /// </summary>
        public OutputProfile OutputProfile { get; set; }

        /// <summary>
        /// <para>When <c>true</c>, whole-line directives (<c>@using</c>, <c>@model</c>, <c>@profile</c>,
        /// <c>@import</c>, definitions, <c>@&lt;&lt;</c> imports, whole-line comments, and any extension block
        /// removed at compile time) swallow their line — leading indentation, trailing spaces, and one line
        /// terminator.</para>
        /// <para>Default: <c>true</c> (2.0) — set <c>false</c> to keep whole-line directives' lines. Participates in
        /// <see cref="Equals(TemplateOptions)"/>/<see cref="GetHashCode"/> — trimming changes output bytes, so
        /// it keys template caches. Compile-time only; never read at render.</para>
        /// </summary>
        public bool TrimDirectiveLines { get; set; }

        public TemplateOptions()
        {
            FileNamePostfix = string.Empty;
            RootPath = AppContext.BaseDirectory;
            TemplateName = string.Empty;
            EnableFileChangeCheck = false;
            ExpressionMode = ExpressionMode.Native;
            MaxRecursionCount = 100;
            OutputProfile = OutputProfile.Html;
            TrimDirectiveLines = true;
        }

        public TemplateOptions(string templateName) {
            FileNamePostfix = string.Empty;
            RootPath = AppContext.BaseDirectory;
            TemplateName = templateName ?? string.Empty;
            EnableFileChangeCheck = false;
            ExpressionMode = ExpressionMode.Native;
            MaxRecursionCount = 100;
            OutputProfile = OutputProfile.Html;
            TrimDirectiveLines = true;
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
        }

        public string FullPath => RootPath + TemplateName + FileNamePostfix;

        public bool Equals(TemplateOptions other)
        {
            return other.FileNamePostfix == FileNamePostfix && other.TemplateName == TemplateName && other.RootPath == RootPath && other.OutputProfile == OutputProfile && other.TrimDirectiveLines == TrimDirectiveLines;
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
                return ((((TemplateName?.GetHashCode() ?? 0) * 397) ^ (int) OutputProfile) * 397) ^ (TrimDirectiveLines ? 1 : 0);
            }
        }
    }
}