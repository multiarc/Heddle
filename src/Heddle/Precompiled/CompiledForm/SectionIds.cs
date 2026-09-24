namespace Heddle.Precompiled.CompiledForm
{
    /// <summary>The section ids of the compiled-form container, in container order. An unknown id is skipped
    /// by length on read; a missing required id is a malformed artifact.</summary>
    internal static class SectionIds
    {
        internal const int Header = 1;
        internal const int Strings = 2;
        internal const int Types = 3;
        internal const int Extensions = 4;
        internal const int Functions = 5;
        internal const int Members = 6;
        internal const int Expressions = 7;
        internal const int CSharp = 8;
        internal const int Documents = 9;
        internal const int Definitions = 10;
        internal const int Templates = 11;
        internal const int Sites = 12;

        internal const int RequiredCount = 12;

        internal const int First = Header;
        internal const int Last = Sites;
    }
}
