namespace Heddle.Extensions
{
    /// <summary>
    /// Shared truthiness for the branch protocol, extracted verbatim from the historical
    /// <c>IfExtension</c>/<c>IfNotExtension</c> semantics: <c>null</c> is false, a <c>bool</c> is itself,
    /// any other non-null value is true. Single-sourced so <c>@if</c>, <c>@ifnot</c>, and <c>@elif</c>
    /// can never diverge.
    /// </summary>
    internal static class BranchCondition
    {
        internal static bool IsTruthy(object value)
        {
            return value != null && (!(value is bool b) || b);
        }
    }
}
