using System;
using System.IO;
using System.Reflection;

namespace Heddle.LanguageServices
{
    /// <summary>What reflecting over a workspace's assemblies can raise when the workspace was built against
    /// something this process does not have — a newer engine's type, attribute or constructor, a dependency that
    /// is not beside it. None of it is the server's fault or the template's: the export or member that needs the
    /// missing piece is skipped and the rest keeps working.</summary>
    internal static class WorkspaceReflection
    {
        internal static bool IsLoadFault(Exception e)
        {
            return e is TypeLoadException || e is MissingMemberException || e is IOException ||
                   e is BadImageFormatException || e is ReflectionTypeLoadException ||
                   e is CustomAttributeFormatException || e is InvalidProgramException ||
                   (e is TargetInvocationException && e.InnerException != null && IsLoadFault(e.InnerException));
        }

        /// <summary>Whether a request may answer with nothing and carry on after <paramref name="e"/>. A request
        /// runs on every keystroke over a document that is broken most of the time, and one that throws is
        /// answered with an error the editor shows or swallows — either way every later request for the same
        /// document fails the same way. Cancellation is the caller's and passes through.</summary>
        internal static bool IsRecoverable(Exception e)
        {
            return !(e is OperationCanceledException || e is OutOfMemoryException ||
                     e is System.Threading.ThreadAbortException || e is AccessViolationException);
        }

        internal static string Describe(Exception e)
        {
            if (e is TargetInvocationException && e.InnerException != null)
                e = e.InnerException;
            string message = e.Message ?? string.Empty;
            int line = message.IndexOfAny(new[] { '\r', '\n' });
            return e.GetType().Name + ": " + (line < 0 ? message : message.Substring(0, line));
        }
    }
}
