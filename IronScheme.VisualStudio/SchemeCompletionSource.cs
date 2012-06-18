using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  class SchemeCompletionSource : ICompletionSource
  {
    SchemeCompletionSourceProvider m_sourceProvider;
    ITextBuffer m_textBuffer;
    List<Completion> m_compList;

    public SchemeCompletionSource(SchemeCompletionSourceProvider sourceProvider, ITextBuffer textBuffer)
    {
      m_sourceProvider = sourceProvider;
      m_textBuffer = textBuffer;
    }

    void ICompletionSource.AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets)
    {
      List<string> strList = new List<string>();

      var bindings = m_textBuffer.Properties["SchemeBindings"] as Dictionary<string, bool>;

      foreach (var key in bindings.Keys)
      {
        strList.Add(key);
      }

      strList.Sort();

      m_compList = new List<Completion>();
      foreach (string str in strList)
        m_compList.Add(new Completion(str, str, str, null, null));

      completionSets.Add(new CompletionSet(
          "Tokens",    //the non-localized title of the tab
          "Tokens",    //the display title of the tab
          FindTokenSpanAtPosition(session.GetTriggerPoint(m_textBuffer),
              session),
          m_compList,
          null));
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
