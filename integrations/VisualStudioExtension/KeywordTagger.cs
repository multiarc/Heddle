using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Templater.VisualStudioExtension {

   static class Constants {
      public const string ClassifName = "Keyword - Flow Control";
      public const string LinqClassifName = "Operator - LINQ";
      public const string VisibilityClassifName = "Keyword - Visibility";
      public const string StringEscapeClassifName = "string Escape Sequence";
      public const string TtlTemplateClassifName = "THTML Template";
   }

   [Export(typeof(IViewTaggerProvider))]
   [ContentType(CSharp.ContentType)]
   [ContentType(JScript.ContentType)]
   [ContentType(JScript.ContentTypeVs2012)]
   [TagType(typeof(ClassificationTag))]
   public class KeywordTaggerProvider : IViewTaggerProvider {
      [Import]
      internal IClassificationTypeRegistryService ClassificationRegistry = null;
      [Import]
      internal IBufferTagAggregatorFactoryService Aggregator = null;

      public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag {
         return new KeywordTagger(
            ClassificationRegistry,
            Aggregator.CreateTagAggregator<IClassificationTag>(buffer)
         ) as ITagger<T>;
      }
   }

   class KeywordTagger : ITagger<ClassificationTag> {
      private readonly ClassificationTag _keywordClassification;
      private readonly ClassificationTag _linqClassification;
      private readonly ClassificationTag _visClassification;
      private readonly ClassificationTag _stringEscapeClassification;
      private readonly ITagAggregator<IClassificationTag> _aggregator;
      private static readonly IList<ClassificationSpan> EmptyList = 
         new List<ClassificationSpan>();

      public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

      internal KeywordTagger(
            IClassificationTypeRegistryService registry, 
            ITagAggregator<IClassificationTag> aggregator) {
         _keywordClassification = 
            new ClassificationTag(registry.GetClassificationType(Constants.ClassifName));
         _linqClassification = 
            new ClassificationTag(registry.GetClassificationType(Constants.LinqClassifName));
         _visClassification = 
            new ClassificationTag(registry.GetClassificationType(Constants.VisibilityClassifName));
         _stringEscapeClassification = 
            new ClassificationTag(registry.GetClassificationType(Constants.StringEscapeClassifName));
         this._aggregator = aggregator;
      }

      public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
         if ( spans.Count == 0 ) {
            yield break;
         }
         foreach (var tagSpan in LookForStringEscapeSequences(spans)) {
             yield return tagSpan;
         }
         foreach (var tagSpan in LookForKeywords(spans)) {
             yield return tagSpan;
         }
      }

      private IEnumerable<ITagSpan<ClassificationTag>> LookForKeywords(NormalizedSnapshotSpanCollection spans) {
         ITextSnapshot snapshot = spans[0].Snapshot;
         LanguageKeywords keywords =
            GetKeywordsByContentType(snapshot.TextBuffer.ContentType);
         if ( keywords == null ) {
            yield break;
         }

         // find spans that the language service has already classified as keywords ...
         var classifiedSpans = GetClassifiedSpans(spans, "keyword");

         // ... and from those, ones that match our keywords
         foreach ( var cs in classifiedSpans ) {
            string text = cs.GetText();
            if ( keywords.ControlFlow.Contains(text) ) {
               yield return new TagSpan<ClassificationTag>(cs, _keywordClassification);
            } else if ( keywords.Visibility.Contains(text) ) {
               yield return new TagSpan<ClassificationTag>(cs, _visClassification);
            } else if ( keywords.Linq.Contains(text) ) {
               yield return new TagSpan<ClassificationTag>(cs, _linqClassification);
            }
         }
      }
      private IEnumerable<ITagSpan<ClassificationTag>> LookForStringEscapeSequences(NormalizedSnapshotSpanCollection spans) {
         ITextSnapshot snapshot = spans[0].Snapshot;
         var classifiedSpans = GetClassifiedSpans(spans, "string");

         foreach ( var cs in classifiedSpans ) {
            string text = cs.GetText();
            // don't process verbatim strings
            if ( text.StartsWith("@") ) continue;
            int start = 1;
            while ( start < text.Length-2 ) {
               if ( text[start] == '\\' ) {
                  int len = 1;
                  int maxlen = Int32.MaxValue;
                  char f = text[start + 1];
                  // not perfect, but close enough for first version
                  if ( f == 'x' || f == 'X' || f == 'u' || f == 'U' ) {
                     while ( (start+len) < text.Length && IsHexDigit(text[start+len+1]) ) {
                        len++;
                     }
                  }
                  if ( f == 'u' ) maxlen = 4;
                  if ( f == 'U' ) maxlen = 8;
                  if ( len > maxlen ) len = maxlen;
                  var sspan = new SnapshotSpan(snapshot, cs.Start.Position+start, len+1);
                  yield return new TagSpan<ClassificationTag>(sspan, _stringEscapeClassification);
                  start += len;
               }
               start++;
            }
         }
      }

      private bool IsHexDigit(char c) {
         if ( Char.IsDigit(c) ) return true;
         return (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
      }

      private IEnumerable<SnapshotSpan> GetClassifiedSpans(NormalizedSnapshotSpanCollection spans, string tagName) {
         ITextSnapshot snapshot = spans[0].Snapshot;
         var mappedSpans =
            from tagSpan in _aggregator.GetTags(spans)
            let name = tagSpan.Tag.ClassificationType.Classification.ToLower()
            where name.Contains(tagName)
            select tagSpan.Span;
         var classifiedSpans =
            from mappedSpan in mappedSpans
            let cs = mappedSpan.GetSpans(snapshot)
            where cs.Count > 0
            select cs[0];
         return classifiedSpans;
      }

      private LanguageKeywords GetKeywordsByContentType(IContentType contentType) {
         if (contentType.IsOfType(CSharp.ContentType)) {
            return new CSharp();
         } else if ( contentType.IsOfType(JScript.ContentType) 
                  || contentType.IsOfType(JScript.ContentTypeVs2012) ) {
            return new JScript();
         }
         // VS is calling us for the "CSharp Signature Help" content-type
         // which we didn't ask for. Argh!!!
         // throw new InvalidOperationException("Running into an unsupported editor");
         return null;
      }
   }
}
