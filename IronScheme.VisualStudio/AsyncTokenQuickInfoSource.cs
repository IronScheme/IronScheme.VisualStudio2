using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IronScheme.Compiler;
using IronScheme.Runtime;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(IAsyncQuickInfoSourceProvider))]
  [ContentType("scheme")]
  class AsyncTokenQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
  {
    [Import]
    IBufferTagAggregatorFactoryService aggService = null;

    public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
      return textBuffer.Properties.GetOrCreateSingletonProperty(
        () => new AsyncTokenQuickInfoSource(textBuffer, aggService.CreateTagAggregator<SchemeTag>(textBuffer)));
    }
  }

  /// <summary>
  /// Provide QuickInfo text for pkgdef string-substitution tokens
  /// </summary>
  class AsyncTokenQuickInfoSource : IAsyncQuickInfoSource
  {
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;

    public AsyncTokenQuickInfoSource(ITextBuffer buffer, ITagAggregator<SchemeTag> aggregator)
    {
      _aggregator = aggregator;
      _buffer = buffer;
    }

    public void Dispose()
    {
      
    }

    public Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken)
    {
      var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(_buffer.CurrentSnapshot);

      if (triggerPoint == null)
      {
        return Task.FromResult<QuickInfoItem>(null);
      }

      // find each span that looks like a token and look it up in the dictionary
      foreach (var curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint)))
      {
        if (curTag.Tag.type == Tokens.SYMBOL)
        {
          SnapshotSpan tagSpan = curTag.Span.GetSpans(_buffer).First();

          var text = tagSpan.GetText();

          if (_buffer.TryGetEnvironment(out var env) && _buffer.TryGetBindings(out var bindings))
          {
            if (bindings.TryGetValue(text, out var type) && type == BindingType.Procedure)
            {
              try
              {
                var proc = ("(eval '" + text + " {0})").Eval(env);
                var forms = "(get-forms {0} {1})".Eval<string>(proc, text).Trim();

                var applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                
                return Task.FromResult(new QuickInfoItem(applicableToSpan, forms));
              }
              catch (SchemeException)
              {
              }
            }
          }
        }
      }

      return Task.FromResult<QuickInfoItem>(null);
    }
  }
}
