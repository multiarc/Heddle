using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using Templates.Core.CompilerServices;
using Templates.Core.Data;

namespace TTL.Classification
{
    [Export(typeof(IClassifier))]
    [ContentType(Configuration.ContentType)]
    public class TTLClassifier : IClassifier
    {
        private readonly IDictionary<TokenType, IClassificationType> _ttlTypes;
        private readonly ITextBuffer _buffer;
        private List<ClassificationSpan> _classificationSpans;

        internal TTLClassifier(ITextBuffer buffer,IClassificationTypeRegistryService typeService)
        {
            _buffer = buffer;
            _buffer.Changed += Changed;
            _ttlTypes = new Dictionary<TokenType, IClassificationType>();
            _ttlTypes[TokenType.StartTTL] = typeService.GetClassificationType(ParserConfiguration.StartTTL);
            _ttlTypes[TokenType.EndTTL] = typeService.GetClassificationType(ParserConfiguration.EndTTL);
            _ttlTypes[TokenType.StartExtensionsBlock] = typeService.GetClassificationType(ParserConfiguration.StartExtensionsBlock);
            _ttlTypes[TokenType.EndExtensionsBlock] = typeService.GetClassificationType(ParserConfiguration.EndExtensionsBlock);
            _ttlTypes[TokenType.ExtensionDelimeter] = typeService.GetClassificationType(ParserConfiguration.ExtensionDelimeter);
            _ttlTypes[TokenType.StartParameter] = typeService.GetClassificationType(ParserConfiguration.StartParameter);
            _ttlTypes[TokenType.EndParameter] = typeService.GetClassificationType(ParserConfiguration.EndParameter);
            _ttlTypes[TokenType.ValidIdentifier] = typeService.GetClassificationType("ID");
            _ttlTypes[TokenType.TemplateBlock] = typeService.GetClassificationType("Block");
            Reparse();
        }

        private void Reparse()
        {
            _classificationSpans = (from token in LexisParser.GetTemplateBlocks(_buffer.CurrentSnapshot.GetText())
                                    select new ClassificationSpan
                                    (new SnapshotSpan(_buffer.CurrentSnapshot, token.StartIndex, token.Length), _ttlTypes[token.Type]))
                                    .ToList();
        }

        private void Changed (object sender, TextContentChangedEventArgs e)
        {
            Reparse();
            ClassificationChanged(sender, new ClassificationChangedEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }

        #region Implementation of IClassifier

        public IList<ClassificationSpan> GetClassificationSpans (SnapshotSpan span)
        {
            return _classificationSpans;
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        #endregion
    }
}