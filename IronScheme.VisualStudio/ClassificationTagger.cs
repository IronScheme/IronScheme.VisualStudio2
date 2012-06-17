using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Media;
using IronScheme.Compiler;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Language.StandardClassification;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Shell;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(ClassificationTag))]
  internal class ClassificationTaggerProvider : ITaggerProvider
  {
    [Export]
    [Name("scheme")]
    [DisplayName("scheme")]
    [BaseDefinition("code")]
    public static ContentTypeDefinition SchemeContentType = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".sls")]
    public static FileExtensionToContentTypeDefinition SlsFileExtension = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".sps")]
    public static FileExtensionToContentTypeDefinition SpsFileExtension = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".ss")]
    public static FileExtensionToContentTypeDefinition SsFileExtension = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".scm")]
    public static FileExtensionToContentTypeDefinition ScmFileExtension = null;

    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry = null; // Set via MEF
    [Import]
    internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        delegate { return new ClassificationTagger(buffer, aggregatorFactory, ClassificationRegistry) as ITagger<T>; });
    }
  }

  class ClassificationTagger : ITagger<ClassificationTag>
  {
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;
    IDictionary<Tokens, IClassificationType> _schemeTokenTypes = new Dictionary<Tokens, IClassificationType>();

    internal ClassificationTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory, IClassificationTypeRegistryService registry)
    {
      _buffer = buffer;
      _aggregator = aggregatorFactory.CreateTagAggregator<SchemeTag>(buffer);

      _schemeTokenTypes[Tokens.COMMENT] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
      _schemeTokenTypes[Tokens.CHARACTER] = registry.GetClassificationType(PredefinedClassificationTypeNames.Character);
      _schemeTokenTypes[Tokens.DIRECTIVE] = registry.GetClassificationType(PredefinedClassificationTypeNames.PreprocessorKeyword);
      _schemeTokenTypes[Tokens.LITERAL] = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
      _schemeTokenTypes[Tokens.NUMBER] = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
      _schemeTokenTypes[Tokens.STRING] = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
      _schemeTokenTypes[Tokens.SYMBOL] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
      _schemeTokenTypes[Tokens.MLSTRING] = registry.GetClassificationType(PredefinedClassificationTypeNames.String);

      _aggregator.BatchedTagsChanged += _aggregator_BatchedTagsChanged;
    }

    void _aggregator_BatchedTagsChanged(object sender, BatchedTagsChangedEventArgs e)
    {
      foreach (var span in e.Spans)
      {
        if (TagsChanged != null)
        {
          TagsChanged(this, new SnapshotSpanEventArgs(span.GetSpans(_buffer)[0]));
        }
      }
    }

    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      foreach (var tagSpan in this._aggregator.GetTags(spans))
      {
        if (_schemeTokenTypes.ContainsKey(tagSpan.Tag.type))
        {
          var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)[0];
          yield return
              new TagSpan<ClassificationTag>(tagSpans,
                                         new ClassificationTag(_schemeTokenTypes[tagSpan.Tag.type]));
        }
      }
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
  }
}
