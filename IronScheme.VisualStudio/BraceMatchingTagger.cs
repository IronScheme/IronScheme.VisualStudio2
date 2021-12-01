using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using IronScheme.VisualStudio.Errors;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(IViewTaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(TextMarkerTag))]
  internal class BraceMatchingTaggerProvider : IViewTaggerProvider
  {
    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
      if (textView == null)
        return null;

      //provide highlighting only on the top-level buffer
      if (textView.TextBuffer != buffer)
        return null;

      return new BraceMatchingTagger(textView, buffer) as ITagger<T>;
    }
  }

  class BraceMatchingTagger : ITagger<TextMarkerTag>
  {
    ITextView View { get; set; }
    ITextBuffer SourceBuffer { get; set; }
    SnapshotPoint? CurrentChar { get; set; }

    internal BraceMatchingTagger(ITextView view, ITextBuffer sourceBuffer)
    {
      this.View = view;
      this.SourceBuffer = sourceBuffer;
      this.CurrentChar = null;
      
      if (!sourceBuffer.Properties.ContainsProperty(SnapshotSpanPair.BraceKey))
      {
        sourceBuffer.Properties[SnapshotSpanPair.BraceKey] = new List<SnapshotSpanPair>();
      }

      this.View.Caret.PositionChanged += CaretPositionChanged;
      this.View.LayoutChanged += ViewLayoutChanged;
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

    void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
    {
      if (e.NewSnapshot != e.OldSnapshot) //make sure that there has really been a change
      {
        UpdateAtCaretPosition(View.Caret.Position);
      }
    }

    void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e)
    {
      UpdateAtCaretPosition(e.NewPosition);
    }

    void UpdateAtCaretPosition(CaretPosition caretPosition)
    {
      CurrentChar = caretPosition.Point.GetPoint(SourceBuffer, caretPosition.Affinity);

      if (!CurrentChar.HasValue)
        return;

      var tempEvent = TagsChanged;
      if (tempEvent != null)
        tempEvent(this, new SnapshotSpanEventArgs(new SnapshotSpan(SourceBuffer.CurrentSnapshot, 0,
            SourceBuffer.CurrentSnapshot.Length)));
    }

    public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      if (spans.Count == 0)   //there is no content in the buffer
        yield break;

      //don't do anything if the current SnapshotPoint is not initialized or at the end of the buffer
      if (!CurrentChar.HasValue || CurrentChar.Value.Position >= CurrentChar.Value.Snapshot.Length)
        yield break;

      //hold on to a snapshot of the current character
      SnapshotPoint currentChar = CurrentChar.Value;

      //if the requested snapshot isn't the same as the one the brace is on, translate our spans to the expected snapshot
      if (spans[0].Snapshot != currentChar.Snapshot)
      {
        currentChar = currentChar.TranslateTo(spans[0].Snapshot, PointTrackingMode.Positive);
      }
      if (currentChar.Position > 0)
      {
        currentChar -= 1;
      }

      var bracelist = SourceBuffer.Properties[SnapshotSpanPair.BraceKey] as List<SnapshotSpanPair>;

      foreach (var pair in bracelist)
      {
        if (pair.Start.IsEmpty || pair.End.IsEmpty)
        {
          continue;
        }
        if (pair.Start.Snapshot != currentChar.Snapshot)
        {
          pair.Start = pair.Start.TranslateTo(currentChar.Snapshot, SpanTrackingMode.EdgeExclusive);
        }
        if (pair.End.Snapshot != currentChar.Snapshot)
        {
          pair.End = pair.End.TranslateTo(currentChar.Snapshot, SpanTrackingMode.EdgeExclusive);
        }

        if (pair.Start.Contains(currentChar) || pair.End.Contains(currentChar))
        {
          yield return new TagSpan<TextMarkerTag>(pair.Start, new TextMarkerTag("brace matching"));
          yield return new TagSpan<TextMarkerTag>(pair.End, new TextMarkerTag("brace matching"));

          break;
        }
      }
    }
  }

}