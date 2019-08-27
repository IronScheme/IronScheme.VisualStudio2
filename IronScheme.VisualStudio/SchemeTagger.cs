using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using IronScheme.Compiler;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(SchemeTag))]
  class SchemeTaggerProvider : ITaggerProvider
  {
    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(() => new SchemeTagger(buffer) as ITagger<T>);
    }
  }

  public class SchemeTag : ITag
  {
    public Tokens type { get; private set; }
    public string ErrorMessage { get; set; }

    public SchemeTag(Tokens type)
    {
      this.type = type;
    }
  }

  class SchemeTagger : ITagger<SchemeTag>
  {
    ITextBuffer buffer;
    List<int[]> states = new List<int[]>();
    static readonly int[] DEFAULTSTATE = { 0 };

    public SchemeTagger (ITextBuffer buffer)
	  {
      this.buffer = buffer;
      buffer.Changed += buffer_Changed;

      for (int i = 0; i < buffer.CurrentSnapshot.LineCount; i++)
      {
        states.Add(DEFAULTSTATE);
      }
	  }

    void buffer_Changed(object sender, TextContentChangedEventArgs e)
    {
      try
      {
        foreach (var chg in e.Changes)
        {
          if (chg.LineCountDelta == 0)
          {
            continue;
          }
          else if (chg.LineCountDelta > 0)
          {
            var ss = buffer.CurrentSnapshot;
            for (int i = 0; i < chg.LineCountDelta; i++)
            {
              int linenr = ss.GetLineNumberFromPosition(chg.NewPosition);
              states.Insert(linenr + i, DEFAULTSTATE);
            }
          }
          else
          {
            var ss = buffer.CurrentSnapshot;
            for (int i = 0; i > chg.LineCountDelta; i--)
            {
              int linenr = ss.GetLineNumberFromPosition(chg.NewPosition);
              states.RemoveAt(linenr - i);
            }
          }
        }
      }
      catch (ArgumentOutOfRangeException)
      {

      }
    }

    public IEnumerable<ITagSpan<SchemeTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      Scanner s = new Scanner { maxParseToken = int.MaxValue };
      foreach (var span in spans)
      {
        var spank = span;

        while (spank.Start <= span.End)
        {
          var thisline = spank.Start.GetContainingLine();
          var linenr = thisline.LineNumber;

          if (linenr > 0)
          {
            int[] state = states[linenr - 1];
            s.Stack = state;
          }

          var text = thisline.GetTextIncludingLineBreak();
          s.SetSource(text, 0);

          Tokens t = Tokens.error;

          while ((t = (Tokens)s.yylex()) != Tokens.EOF)
          {
            if (s.yytext.Length > 0)
            {
              var start = thisline.Start + s.yylloc.sCol;
              yield return new TagSpan<SchemeTag>(
                new SnapshotSpan(start, s.yylloc.eCol - s.yylloc.sCol),
                new SchemeTag(t) { ErrorMessage = s.ErrorMessage });
            }
            else
            {
              break;
            }
          }

          var stack = s.Stack;
          var prevstate = states[linenr];

          states[linenr] = stack;

          if (stack.Length != prevstate.Length && (linenr + 1) < spank.Snapshot.LineCount)
          {
            // carry on till state.Length == 1 (actually need to compare both stacks)
            spank = spank.Snapshot.GetLineFromLineNumber(linenr + 1).ExtentIncludingLineBreak;
            if (TagsChanged != null)
            {
              TagsChanged(this, new SnapshotSpanEventArgs(spank));
            }
          }
          else if (thisline.End < span.End)
          {
            spank = spank.Snapshot.GetLineFromLineNumber(linenr + 1).ExtentIncludingLineBreak;
          }
          else
          {
            break;
          }
        }
      }
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
  }
}
