using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IronScheme.VisualStudio
{
  class SchemeCompletionSource : ICompletionSource
  {
    SchemeCompletionSourceProvider m_sourceProvider;
    ITextBuffer m_textBuffer;
    List<Completion> m_compList;
    static ImageSource syntax_image, procedure_image;

    static SchemeCompletionSource()
    {
      var ass = typeof(SchemeCompletionSource).Assembly;
      var img = new PngBitmapDecoder(ass.GetManifestResourceStream("IronScheme.VisualStudio.Resources.CodeField.png"),
        BitmapCreateOptions.None, BitmapCacheOption.Default);
      syntax_image = img.Frames[0];
      img = new PngBitmapDecoder(ass.GetManifestResourceStream("IronScheme.VisualStudio.Resources.CodeMethod.png"),
        BitmapCreateOptions.None, BitmapCacheOption.Default);
      procedure_image = img.Frames[0];
    }

    public SchemeCompletionSource(SchemeCompletionSourceProvider sourceProvider, ITextBuffer textBuffer)
    {
      m_sourceProvider = sourceProvider;
      m_textBuffer = textBuffer;
    }

    void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
    {
      ITextSnapshot snapshot = m_textBuffer.CurrentSnapshot;
      SnapshotPoint? triggerPoint = session.GetTriggerPoint(snapshot);
      if (triggerPoint == null)
      {
        return;
      }
      SnapshotPoint end = triggerPoint.Value;
      SnapshotPoint start = end;
      // go back to either a delimiter, a whitespace char or start of line.
      while (start > 0)
      {
        SnapshotPoint prev = start - 1;
        if (IsWhiteSpaceOrDelimiter(prev.GetChar()))
        {
          break;
        }
        start += -1;
      }

      var span = new SnapshotSpan(start, end);
      // The ApplicableTo span is what text will be replaced by the completion item
      ITrackingSpan applicableTo = snapshot.CreateTrackingSpan(span, SpanTrackingMode.EdgeInclusive);

      var bindings = m_textBuffer.Properties["SchemeBindings"] as Dictionary<string, bool>;

      m_compList = new List<Completion>();

      foreach (string key in bindings.Keys.OrderBy(x => x))
      {
        var str = key;
        var v = bindings[key];
        m_compList.Add(new Completion(str, str, (v ? "syntax" : "procedure") + ": " + str, v ? syntax_image : procedure_image, null));
      }

      completionSets.Add(new CompletionSet(
          "Tokens",    //the non-localized title of the tab
          "Tokens",    //the display title of the tab
          applicableTo,
          m_compList,
          null));
    }

    static bool IsWhiteSpaceOrDelimiter(char p)
    {
      switch (p)
      {
        case '(':
        case '[':
        case ' ':
          return true;
      }
      return false;
    }

    ITrackingSpan FindTokenSpanAtPosition(ITrackingPoint point, ICompletionSession session)
    {
      SnapshotPoint currentPoint = (session.TextView.Caret.Position.BufferPosition) - 1;
      ITextStructureNavigator navigator = m_sourceProvider.NavigatorService.GetTextStructureNavigator(m_textBuffer);
      TextExtent extent = navigator.GetExtentOfWord(currentPoint);
      return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
    }

    private bool m_isDisposed;
    public void Dispose()
    {
      if (!m_isDisposed)
      {
        GC.SuppressFinalize(this);
        m_isDisposed = true;
      }
    }

  }

  [Export(typeof(ICompletionSourceProvider))]
  [ContentType("scheme")]
  [Name("token completion")]
  class SchemeCompletionSourceProvider : ICompletionSourceProvider
  {
    [Import]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

    public ICompletionSource TryCreateCompletionSource(ITextBuffer textBuffer)
    {
      return new SchemeCompletionSource(this, textBuffer);
    }
  }
}
