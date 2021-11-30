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
 

  [Export(typeof(ITaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(IOutliningRegionTag))]
  internal class OutlineTaggerProvider : ITaggerProvider
  {
    public OutlineTaggerProvider()
    {

    }

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        () => new OutlineTagger(buffer) as ITagger<T>);
    }
  }

  class OutlineTagger : ITagger<IOutliningRegionTag>, IDisposable
  {
    private ITextBuffer buffer;

    public OutlineTagger(ITextBuffer buffer)
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
        var lines = snapshot.Lines.ToArray();
        var loc = ExtractLocation(location);

        var start = lines[loc.Start.Line - 1].Start + (loc.Start.Column - 1);
        var end = lines[loc.End.Line - 1].Start + (loc.End.Column - 1);

        return new SnapshotSpan(start, end);
      }
      catch
      {
        return null;
      }
    }

    IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(Cons c)
    {
      foreach (var item in c)
      {
        if (item is Annotation a)
        {
          foreach (var item2 in GetTags(a))
          {
            yield return item2;
          }
        }
      }
    }

    IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(Annotation a)
    {
      var span = MakeSnapshotSpan(buffer.CurrentSnapshot, (string)((Cons)a.source).cdr);
      if (span.HasValue && span?.Start.GetContainingLine().LineNumber != span?.End.GetContainingLine().LineNumber)
      {
        var header = span?.Start.GetContainingLine().GetText().Trim() + " ...";
        yield return new TagSpan<IOutliningRegionTag>(span.Value, new OutliningRegionTag(header, span?.GetText().Replace("\t", " ")));

        if (a.expression is Cons cc)
        {
          foreach (var item in GetTags(cc))
          {
            yield return item;
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
          foreach (var item in GetTags(c))
          {
            yield return item;
          }
        }
        else if (result is Annotation a)
        {
          foreach (var item in GetTags(a))
          {
            yield return item;
          }
        }
      }
    }
  }
}
