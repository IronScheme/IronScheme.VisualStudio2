using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITextStructureNavigatorProvider))]
  [ContentType("scheme")]
  internal class TextStructureNavigatorProvider : ITextStructureNavigatorProvider
  {
    [Import]
    internal IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService = null;

    public ITextStructureNavigator CreateTextStructureNavigator(ITextBuffer textBuffer)
    {
      return textBuffer.Properties.GetOrCreateSingletonProperty(() => new TextStructureNavigator(textBuffer, BufferTagAggregatorFactoryService));
    }
  }

  class TextStructureNavigator : ITextStructureNavigator
  {
    private ITextBuffer textBuffer;
    private readonly ITagAggregator<SchemeTag> aggregator;

    public TextStructureNavigator(ITextBuffer textBuffer, IBufferTagAggregatorFactoryService aggregatorFactory)
    {
      this.textBuffer = textBuffer;
      aggregator = aggregatorFactory.CreateTagAggregator<SchemeTag>(textBuffer);
    }

    public IContentType ContentType => textBuffer.ContentType;

    public TextExtent GetExtentOfWord(SnapshotPoint currentPosition)
    {
      var span = new SnapshotSpan(currentPosition, 0);

      foreach (var tag in aggregator.GetTags(span))
      {
        SnapshotSpan tagSpan = tag.Span.GetSpans(textBuffer).First();
        return new TextExtent(tagSpan, true);
      }

      return new TextExtent(span, false);
    }

    public SnapshotSpan GetSpanOfEnclosing(SnapshotSpan activeSpan) => activeSpan;

    public SnapshotSpan GetSpanOfFirstChild(SnapshotSpan activeSpan) => activeSpan;

    public SnapshotSpan GetSpanOfNextSibling(SnapshotSpan activeSpan) => activeSpan;

    public SnapshotSpan GetSpanOfPreviousSibling(SnapshotSpan activeSpan) => activeSpan;
  }
 

}
