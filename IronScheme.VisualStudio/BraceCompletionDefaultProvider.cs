using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [BracePair('(', ')')]
  [BracePair('[', ']')]
  [BracePair('"', '"')]
  [ContentType("scheme")]
  [Export(typeof(IBraceCompletionContextProvider))]
  class BraceCompletionContextProvider : IBraceCompletionContextProvider
  {
    public bool TryCreateContext(ITextView textView, SnapshotPoint openingPoint, char openingBrace, char closingBrace, out IBraceCompletionContext context)
    {
      context = new BraceCompletionContext();
      return true;
    }
  }

  class BraceCompletionContext : IBraceCompletionContext
  {
    public bool AllowOverType(IBraceCompletionSession session)
    {
      return true;
    }

    public void Finish(IBraceCompletionSession session)
    {
      
    }

    public void OnReturn(IBraceCompletionSession session)
    {
      
    }

    public void Start(IBraceCompletionSession session)
    {
      
    }
  }


}