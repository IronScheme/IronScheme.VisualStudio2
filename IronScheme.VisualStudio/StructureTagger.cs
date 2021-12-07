using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using IronScheme.Runtime;
using IronScheme.Runtime.psyntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(StructureTag))]
  internal class StructureTaggerProvider : ITaggerProvider
  {
    [Import]
    IBufferTagAggregatorFactoryService aggService = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        () => new StructureTagger(buffer, aggService.CreateTagAggregator<SchemeTag>(buffer)) as ITagger<T>);
    }
  }

  class StructureTagger : BaseTagger, ITagger<StructureTag>, IDisposable
  {
    private ITextBuffer buffer;
    private readonly ITagAggregator<SchemeTag> tagAggregator;

    public StructureTagger(ITextBuffer buffer, ITagAggregator<SchemeTag> tagAggregator)
    {
      this.buffer = buffer;
      this.tagAggregator = tagAggregator;
    }

    IEnumerable<ITagSpan<StructureTag>> GetTags(Cons c, NormalizedSnapshotSpanCollection spans)
    {
      foreach (var item in c)
      {
        if (item is Annotation a)
        {
          foreach (var item2 in GetTags(a, spans))
          {
            yield return item2;
          }
        }
      }
    }

    IEnumerable<ITagSpan<StructureTag>> GetTags(Annotation a, NormalizedSnapshotSpanCollection spans)
    {
      var maybeSpan = MakeSnapshotSpan(buffer.CurrentSnapshot, (string)((Cons)a.source).cdr);
      if (maybeSpan.HasValue)
      {
        var span = maybeSpan.Value;

        if (spans.IntersectsWith(span))
        {
          var startLine = span.Start.GetContainingLine();
          var endLine = span.End.GetContainingLine();

          if (startLine.LineNumber != endLine.LineNumber)
          {
            var headerSpan = new SnapshotSpan(span.Start, startLine.End);
            var header = headerSpan.GetText() + " ... ";
            header += new string(')', header.Count(x => x == '(') - header.Count(x => x == ')'));
            header += new string(']', header.Count(x => x == '[') - header.Count(x => x == ']'));

            var collapsable = Math.Abs(startLine.LineNumber - endLine.LineNumber) > 1;
            var startOffset = span.Start - startLine.Start;
            var endOffset = endLine.End - span.End;
            var collapseHint = new SnapshotSpan(startLine.Start, endLine.End).GetText();
            collapseHint = new string(' ', startOffset) + collapseHint.Substring(startOffset, collapseHint.Length - startOffset - endOffset) + new string(' ', endOffset);

            yield return new TagSpan<StructureTag>(span,
              new StructureTag(buffer.CurrentSnapshot, span, headerSpan, type: PredefinedStructureTagTypes.Expression, isCollapsible: true, collapsedForm: header, collapsedHintForm: collapseHint));
          }
          else
          {
            yield return new TagSpan<StructureTag>(span, new StructureTag(buffer.CurrentSnapshot, span, type: PredefinedStructureTagTypes.Nonstructural));
          }

          if (a.expression is Cons cc)
          {
            foreach (var item in GetTags(cc, spans))
            {
              yield return item;
            }
          }
        }
      }
    }

    public IEnumerable<ITagSpan<StructureTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      if (buffer.TryGetResult(out var result))
      {
        if (result is Cons c)
        {
          foreach (var item in GetTags(c, spans))
          {
            yield return item;
          }
        }
        else if (result is Annotation a)
        {
          foreach (var item in GetTags(a, spans))
          {
            yield return item;
          }
        }
      }

      var lines = 0;
      IMappingTagSpan<SchemeTag> start = null, last = null;

      foreach (var tag in this.tagAggregator.GetTags(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length)))
      {
        if (tag.Tag.type == Compiler.Tokens.COMMENT)
        {
          if (start == null)
          {
            start = tag;
          }
          lines++;
          last = tag;
          continue;
        }

        if (start != null)
        {
          if (lines > 2)
          {
            var ss = start.Span.GetSpans(buffer).First();
            var tt = last.Span.GetSpans(buffer).First();
            var cc = new SnapshotSpan(ss.Start, tt.End);

            lines = 0;

            if (spans.IntersectsWith(cc))
            {

              var header = ss.Start.GetContainingLine();
              var headerText = header.GetText();
              var headerSpan = new SnapshotSpan(ss.Start, header.End);

              yield return new TagSpan<StructureTag>(cc, 
                new StructureTag(buffer.CurrentSnapshot, cc, headerSpan, type: PredefinedStructureTagTypes.Comment, isCollapsible: true, collapsedForm: headerText, collapsedHintForm: cc.GetText()));
            }
          }

          start = null;
          last = tag;
        }
      }
    }
  }
}
