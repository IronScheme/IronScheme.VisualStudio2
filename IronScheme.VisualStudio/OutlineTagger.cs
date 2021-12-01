﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using IronScheme.Runtime;
using IronScheme.Runtime.psyntax;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(IOutliningRegionTag))]
  internal class OutlineTaggerProvider : ITaggerProvider
  {
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        () => new OutlineTagger(buffer) as ITagger<T>);
    }
  }

  class OutlineTagger : BaseTagger, ITagger<IOutliningRegionTag>
  {
    private ITextBuffer buffer;

    public OutlineTagger(ITextBuffer buffer)
    {
      this.buffer = buffer;
    }

    IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(Cons c, NormalizedSnapshotSpanCollection spans)
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

    IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(Annotation a, NormalizedSnapshotSpanCollection spans)
    {
      var maybeSpan = MakeSnapshotSpan(buffer.CurrentSnapshot, (string)((Cons)a.source).cdr);
      if (maybeSpan.HasValue)
      { 
        var span = maybeSpan.Value;
        if (spans.IntersectsWith(span))
        {
          var startLine = span.Start.GetContainingLine();

          if (Math.Abs(startLine.LineNumber - span.End.GetContainingLine().LineNumber) > 1)
          {
            var header = startLine.GetText().Substring(span.Start - startLine.Start) + " ... ";

            header += new string(')', header.Count(x => x == '(') - header.Count(x => x == ')'));
            header += new string(']', header.Count(x => x == '[') - header.Count(x => x == ']'));
            yield return new TagSpan<IOutliningRegionTag>(span, new OutliningRegionTag(header, span.GetText().Replace("\t", " ")));

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
    }

    public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      if (buffer.Properties.TryGetProperty<object>("Result", out var result))
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
    }
  }
}
