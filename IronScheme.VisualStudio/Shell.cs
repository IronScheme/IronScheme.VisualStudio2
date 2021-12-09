using System.Collections.Generic;
using System.Linq;
using IronScheme.Runtime;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace IronScheme.VisualStudio
{
    public static class Shell
    {
        internal static ITextDocumentFactoryService TextDocumentFactory { get; set; }
        internal static HashSet<ITextView> Views { get; } = new HashSet<ITextView>();

        public static Cons OpenViews() => Cons.FromList(Views.Select(v => TextDocumentFactory.TryGetTextDocument(v.TextBuffer, out var doc) ? doc.FilePath : v.ToString()));
    }
}
