using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(IIntellisenseControllerProvider))]
  [ContentType("scheme")]
  [Name("SchemeQuickInfoControllerProvider")]
  class SchemeQuickInfoControllerProvider : IIntellisenseControllerProvider
  {
    [Import]
    internal IQuickInfoBroker QuickInfoBroker { get; set; }

    public IIntellisenseController TryCreateIntellisenseController(ITextView textView,
        IList<ITextBuffer> subjectBuffers)
    {
      return new SchemeQuickInfoController(textView, subjectBuffers, this);
    }
  }

  class SchemeQuickInfoController : IIntellisenseController
  {
    ITextView _textView;
    IList<ITextBuffer> _subjectBuffers;
    SchemeQuickInfoControllerProvider _componentContext;

    IQuickInfoSession _session;

    internal SchemeQuickInfoController(ITextView textView, IList<ITextBuffer> subjectBuffers, SchemeQuickInfoControllerProvider componentContext)
    {
      _textView = textView;
      _subjectBuffers = subjectBuffers;
      _componentContext = componentContext;

      _textView.MouseHover += this.OnTextViewMouseHover;
    }

    public void ConnectSubjectBuffer(ITextBuffer subjectBuffer)
    {
    }

    public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer)
    {
    }

    public void Detach(ITextView textView)
    {
      if (_textView == textView)
      {
        _textView.MouseHover -= this.OnTextViewMouseHover;
        _textView = null;
      }
    }
    
    void OnTextViewMouseHover(object sender, MouseHoverEventArgs e)
    {
      SnapshotPoint? point = this.GetMousePosition(new SnapshotPoint(_textView.TextSnapshot, e.Position));

      if (point != null)
      {
        ITrackingPoint triggerPoint = point.Value.Snapshot.CreateTrackingPoint(point.Value.Position,
            PointTrackingMode.Positive);

        // Find the broker for this buffer

        if (!_componentContext.QuickInfoBroker.IsQuickInfoActive(_textView))
        {
          _session = _componentContext.QuickInfoBroker.CreateQuickInfoSession(_textView, triggerPoint, true);
          _session.Start();
        }
      }
    }

    SnapshotPoint? GetMousePosition(SnapshotPoint topPosition)
    {
      // Map this point down to the appropriate subject buffer.

      return _textView.BufferGraph.MapDownToFirstMatch
          (
          topPosition,
          PointTrackingMode.Positive,
          snapshot => _subjectBuffers.Contains(snapshot.TextBuffer),
          PositionAffinity.Predecessor
          );
    }
  }
}
