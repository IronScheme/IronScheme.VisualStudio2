using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IronScheme.Runtime;
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
  [Name("AsyncCompletionSourceProvider")]
  class AsyncCompletionSourceProvider : IAsyncCompletionSourceProvider
  {
    [Import]
    internal ITextStructureNavigatorSelectorService NavigatorService { get; set; }

    public IAsyncCompletionSource GetOrCreate(ITextView textView)
    {
        return new AsyncCompletionSource(this);
    }
  }

  class AsyncCompletionSource : IAsyncCompletionSource
  {
    AsyncCompletionSourceProvider m_sourceProvider;

    public AsyncCompletionSource(AsyncCompletionSourceProvider sourceProvider)
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

      if (buffer.TryGetBindings(out cache))
      {
        var items = new List<CompletionItem>();

        foreach (string key in cache.Keys.OrderBy(x => x))
        {
          var str = key;
          var v = cache[key];
          if (v != BindingType.Record)
          {
            var icon = new ImageElement((v == BindingType.Procedure ? KnownMonikers.Method : KnownMonikers.Class).ToImageId());
            items.Add(new CompletionItem(str, this, icon));
          }
        }

        var cc = new CompletionContext(items.ToImmutableArray());
        return cc;
      }
      else
      {
        return null;
      }
    }

    public async Task<object> GetDescriptionAsync(IAsyncCompletionSession session, CompletionItem item, CancellationToken token)
    {
      var text = item.DisplayText;
      var buffer = session.ApplicableToSpan.TextBuffer;

      if (buffer.TryGetEnvironment(out var env))
      {
        if (cache.TryGetValue(text, out var type) && type == BindingType.Procedure)
        {
          try
          {
            var proc = ("(eval '" + text + " {0})").Eval(env);
            var forms = "(get-forms {0} {1})".Eval<string>(proc, text).Trim();

            return forms;
          }
          catch (SchemeException)
          {
          }
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
      var prefix = new SnapshotSpan(tokenSpan.Start, triggerLocation);
      return new CompletionStartData(CompletionParticipation.ProvidesItems, prefix);
    }
  }

}
