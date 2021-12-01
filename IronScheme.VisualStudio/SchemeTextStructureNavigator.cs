using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text.RegularExpressions;
using IronScheme.Runtime;
using IronScheme.Runtime.psyntax;
using IronScheme.Scripting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text.UI.Adornments;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITextStructureNavigatorProvider))]
  [ContentType("scheme")]
  internal class SchemeTextStructureNavigatorProvider : ITextStructureNavigatorProvider
  {
    [Import]
    internal IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService = null;

    public ITextStructureNavigator CreateTextStructureNavigator(ITextBuffer textBuffer)
    {
      return textBuffer.Properties.GetOrCreateSingletonProperty(() => new SchemeTextStructureNavigator(textBuffer, BufferTagAggregatorFactoryService));
    }
  }

  class SchemeTextStructureNavigator : ITextStructureNavigator
  {
    private ITextBuffer textBuffer;
    private readonly ITagAggregator<SchemeTag> aggregator;

    public SchemeTextStructureNavigator(ITextBuffer textBuffer, IBufferTagAggregatorFactoryService aggregatorFactory)
    {
      this.textBuffer = textBuffer;
      aggregator = aggregatorFactory.CreateTagAggregator<SchemeTag>(textBuffer);
    }

    public IContentType ContentType => textBuffer.ContentType;

    public TextExtent GetExtentOfWord(SnapshotPoint currentPosition)
    {
      foreach(var tag in aggregator.GetTags(new SnapshotSpan(currentPosition,0)))
      {
        SnapshotSpan tagSpan = tag.Span.GetSpans(textBuffer).First();
        return new TextExtent(tagSpan, tag.Tag.type == Compiler.Tokens.SYMBOL);
      }

      return new TextExtent();
    }

    public SnapshotSpan GetSpanOfEnclosing(SnapshotSpan activeSpan) => activeSpan;

    public SnapshotSpan GetSpanOfFirstChild(SnapshotSpan activeSpan) => activeSpan;

    public SnapshotSpan GetSpanOfNextSibling(SnapshotSpan activeSpan) => activeSpan;

    public SnapshotSpan GetSpanOfPreviousSibling(SnapshotSpan activeSpan) => activeSpan;
  }
 
  [Export(typeof(IViewTaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(StructureTag))]
  internal class StructureTaggerProvider : IViewTaggerProvider
  {
    public ITagger<T> CreateTagger<T>(ITextView view, ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        () => new StructureTagger(buffer) as ITagger<T>);
    }
  }

  class StructureTagger : ITagger<StructureTag>, IDisposable
  {
    private ITextBuffer buffer;

    public StructureTagger(ITextBuffer buffer)
    {
      this.buffer = buffer;
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    internal void RaiseTagsChanged(SnapshotSpan span)
    {
      var e = new SnapshotSpanEventArgs(span);
      if (TagsChanged != null)
      {
        TagsChanged(this, e);
      }
    }

    public void Dispose()
    {
      
    }

    static readonly Regex LOCATIONMATCH = new Regex(
  @"\((?<startline>\d+),(?<startcol>\d+)\)\s-\s\((?<endline>\d+),(?<endcol>\d+)\)",
  RegexOptions.Compiled);

    static SourceSpan ExtractLocation(string location)
    {
      var m = LOCATIONMATCH.Match(location);

      return new SourceSpan(
        new SourceLocation(0, Convert.ToInt32(m.Groups["startline"].Value), Convert.ToInt32(m.Groups["startcol"].Value)),
        new SourceLocation(0, Convert.ToInt32(m.Groups["endline"].Value), Convert.ToInt32(m.Groups["endcol"].Value)));
    }

    SnapshotSpan? MakeSnapshotSpan(ITextSnapshot snapshot, string location)
    {
      try
      {
        var loc = ExtractLocation(location);

        var start = snapshot.GetLineFromLineNumber(loc.Start.Line - 1).Start + (loc.Start.Column - 1);
        var end = snapshot.GetLineFromLineNumber(loc.End.Line - 1).Start + (loc.End.Column - 1);

        return new SnapshotSpan(start, end);
      }
      catch
      {
        return null;
      }
    }

    IEnumerable<ITagSpan<StructureTag>> GetTags(Cons c, SnapshotSpan span)
    {
      foreach (var item in c)
      {
        if (item is Annotation a)
        {
          foreach (var item2 in GetTags(a, span))
          {
            yield return item2;
          }
        }
      }
    }

    IEnumerable<ITagSpan<StructureTag>> GetTags(Annotation a, SnapshotSpan s)
    {
      var span = MakeSnapshotSpan(buffer.CurrentSnapshot, (string)((Cons)a.source).cdr);
      if (span.HasValue && s.OverlapsWith(span.Value) && span?.Start.GetContainingLine().LineNumber != span?.End.GetContainingLine().LineNumber)
      {
        var header = span?.Start.GetContainingLine().Extent;
        yield return new TagSpan<StructureTag>(span.Value, new StructureTag(buffer.CurrentSnapshot, span, header, type: PredefinedStructureTagTypes.Expression));

        if (a.expression is Cons cc)
        {
          foreach (var item in GetTags(cc, s))
          {
            yield return item;
          }
        }
      }
    }

    public IEnumerable<ITagSpan<StructureTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      if (buffer.Properties.TryGetProperty<object>("Result", out var result))
      {
        foreach (var span in spans)
        {
          if (result is Cons c)
          {
            foreach (var item in GetTags(c, span))
            {
              yield return item;
            }
          }
          else if (result is Annotation a)
          {
            foreach (var item in GetTags(a, span))
            {
              yield return item;
            }
          }
        }
      }
    }
  }
}
