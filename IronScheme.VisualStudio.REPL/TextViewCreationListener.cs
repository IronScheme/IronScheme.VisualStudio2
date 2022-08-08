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
        private readonly ITextDocumentFactoryService textDocumentFactory;

        [ImportingConstructor]
        public TextViewCreationListener(ITextDocumentFactoryService textDocumentFactory, Microsoft.VisualStudio.Text.Operations.IEditorOperationsFactoryService editorOperationsFactoryService)
        {
            this.textDocumentFactory = textDocumentFactory;
            Shell.EditorOperationsFactoryService = editorOperationsFactoryService;
        }

        public void TextViewCreated(ITextView textView)
        {
            if (textDocumentFactory.TryGetTextDocument(textView.TextBuffer, out var doc))
            {
                Shell.Views.Add(doc.FilePath, textView);
                textView.Closed += (s, e) => Shell.Views.Remove(doc.FilePath);
            }
        }
    }
}
