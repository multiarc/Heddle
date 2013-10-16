using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace TTL.Intellisense {
    internal class TTLCompletionSource: ICompletionSource {
        private readonly ITextBuffer _buffer;
        private bool _disposed;

        public TTLCompletionSource (ITextBuffer buffer)
        {
            _buffer = buffer;
        }

        #region ICompletionSource Members

        public void AugmentCompletionSession (ICompletionSession session, IList<CompletionSet> completionSets)
        {
            if (_disposed)
                throw new ObjectDisposedException("TTLCompletionSource");

            var completions = new List<Completion>
            {
                new Completion("Ook!"),
                new Completion("Ook."),
                new Completion("Ook?")
            };

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            var triggerPoint = (SnapshotPoint) session.GetTriggerPoint(snapshot);

            if (triggerPoint == null)
                return;

            ITextSnapshotLine line = triggerPoint.GetContainingLine();
            SnapshotPoint start = triggerPoint;

            while (start > line.Start && !char.IsWhiteSpace((start - 1).GetChar()))
                start -= 1;

            ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(new SnapshotSpan(start, triggerPoint), SpanTrackingMode.EdgeInclusive);

            completionSets.Add(new CompletionSet("All", "All", applicableTo, completions, Enumerable.Empty<Completion>()));
        }

        public void Dispose ()
        {
            _disposed = true;
        }

        #endregion
    }
}