using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IronScheme.Runtime;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Core.Imaging;

namespace IronScheme.VisualStudio
{
  [Export(typeof(IAsyncCompletionSourceProvider))]
  [ContentType("scheme")]
  [Name("token completion")]
  class SchemeCompletionSourceProvider : IAsyncCompletionSourceProvider
  {
    [Import]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

    public IAsyncCompletionSource GetOrCreate(ITextView textView)
    {
        return new SchemeCompletionSource(this);
    }
  }

  class SchemeCompletionSource : IAsyncCompletionSource
  {
    SchemeCompletionSourceProvider m_sourceProvider;

    public SchemeCompletionSource(SchemeCompletionSourceProvider sourceProvider)
    {
      m_sourceProvider = sourceProvider;
    }

    private SnapshotSpan FindTokenSpanAtPosition(SnapshotPoint triggerLocation)
    {
      // This method is not really related to completion,
      // we mostly work with the default implementation of ITextStructureNavigator 
      // You will likely use the parser of your language
      ITextStructureNavigator navigator = m_sourceProvider.NavigatorService.GetTextStructureNavigator(triggerLocation.Snapshot.TextBuffer);
      TextExtent extent = navigator.GetExtentOfWord(triggerLocation);

      var tokenSpan = triggerLocation.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);

      var snapshot = triggerLocation.Snapshot;
      var tokenText = tokenSpan.GetText(snapshot);
      if (string.IsNullOrWhiteSpace(tokenText))
      {
        // The token at this location is empty. Return an empty span, which will grow as user types.
        return new SnapshotSpan(triggerLocation, 0);
      }

      return new SnapshotSpan(tokenSpan.GetStartPoint(snapshot), tokenSpan.GetEndPoint(snapshot));
    }

    public void Dispose()
    {

    }

    Dictionary<string, BindingType> cache = new Dictionary<string, BindingType>();

    public async Task<CompletionContext> GetCompletionContextAsync(IAsyncCompletionSession session, CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableToSpan, CancellationToken token)
    {
      var ss = triggerLocation.Snapshot;
      var buffer = ss.TextBuffer;

      cache = buffer.Properties["SchemeBindings"] as Dictionary<string, BindingType>;

      var items = new List<CompletionItem>();

      foreach (string key in cache.Keys.OrderBy(x => x))
      {
        var str = key;
        var v = cache[key];
        if (v != BindingType.Record)
        {
          var icon = new ImageElement((v == BindingType.Procedure ? KnownMonikers.Method : KnownMonikers.Class).ToImageId());
          items.Add(new CompletionItem(str, this, icon));// str, v.ToString(), v == BindingType.Procedure ? procedure_image : syntax_image, null));
        }
      }

      var cc = new CompletionContext(items.ToImmutableArray());
      return cc;
    }

    public async Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
    {
      var text = item.DisplayText;
      var buffer = session.ApplicableToSpan.TextBuffer;
      var env = buffer.Properties["SchemeEnvironment"];

      BindingType type;

      if (cache.TryGetValue(text, out type) && type == BindingType.Procedure)
      {
        try
        {
          var proc = ("(eval '" + text + " {0})").Eval(env);
          var forms = "(get-forms {0} {1})".Eval<string>(proc, text).Trim();

          return forms;
        }
        catch (SchemeException ex)
        {
        }
      }

      return null;
    }

    public CompletionStartData InitializeCompletion(CompletionTrigger trigger, SnapshotPoint triggerLocation, CancellationToken token)
    {
      if (trigger.Reason != CompletionTriggerReason.InvokeAndCommitIfUnique)
      {
        return CompletionStartData.DoesNotParticipateInCompletion;
      }

      var tokenSpan = FindTokenSpanAtPosition(triggerLocation);
      return new CompletionStartData(CompletionParticipation.ProvidesItems, tokenSpan);
    }
  }

}
