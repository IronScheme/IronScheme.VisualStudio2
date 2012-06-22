using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ISmartIndentProvider))]
  [ContentType("scheme")]
  class SmartIndentProvider : ISmartIndentProvider
  {
    public ISmartIndent CreateSmartIndent(ITextView textView)
    {
      return textView.Properties.GetOrCreateSingletonProperty(() => new SmartIndent());
    }
  }

  class SmartIndent : ISmartIndent
  {
    public int? GetDesiredIndentation(ITextSnapshotLine line)
    {
      if (line.LineNumber == 0) return 0;

      var snapshot = line.Snapshot;
      var prevline = snapshot.GetLineFromLineNumber(line.LineNumber - 1);
      var text = prevline.GetText();
      var ifidx = text.IndexOf("(if ");
      if (ifidx >= 0)
      {
        return ifidx + 4;
      }
      for (int i = 0; i < text.Length; i++)
      {
        if (!char.IsWhiteSpace(text[i]))
        {
          return i;
        }
      }
      return null;
    }

    public void Dispose()
    {
      
    }
  }

}
