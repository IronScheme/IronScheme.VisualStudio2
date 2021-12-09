using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
    [Export(typeof(ITextViewCreationListener))]
    [ContentType("scheme")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    class TextViewCreationListener : ITextViewCreationListener
    {
        [ImportingConstructor]
        public TextViewCreationListener(ITextDocumentFactoryService textDocumentFactory)
        {
            Shell.TextDocumentFactory = textDocumentFactory;
        }

        public void TextViewCreated(ITextView textView)
        {
            Shell.Views.Add(textView);
            textView.Closed += (s, e) => Shell.Views.Remove(textView);
        }
    }
}
