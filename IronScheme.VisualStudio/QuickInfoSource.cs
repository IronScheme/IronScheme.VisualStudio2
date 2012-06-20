using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using IronScheme.Compiler;
using IronScheme.Runtime;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(IQuickInfoSourceProvider))]
  [ContentType("scheme")]
  [Name("SchemeTokenQuickInfoSource")]
  class SchemeTokenQuickInfoSourceProvider : IQuickInfoSourceProvider
  {
    [Import]
    IBufferTagAggregatorFactoryService aggService = null;

    public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer)
    {
      return new SchemeTokenQuickInfoSource(textBuffer, aggService.CreateTagAggregator<SchemeTag>(textBuffer));
    }
  }

  /// <summary>
  /// Provide QuickInfo text for pkgdef string-substitution tokens
  /// </summary>
  class SchemeTokenQuickInfoSource : IQuickInfoSource
  {
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;

    public SchemeTokenQuickInfoSource(ITextBuffer buffer, ITagAggregator<SchemeTag> aggregator)
    {
      _aggregator = aggregator;
      _buffer = buffer;
    }

    public void AugmentQuickInfoSession(IQuickInfoSession session, IList<object> quickInfoContent, out ITrackingSpan applicableToSpan)
    {
      applicableToSpan = null;

      var triggerPoint = (SnapshotPoint)session.GetTriggerPoint(_buffer.CurrentSnapshot);

      if (triggerPoint == null)
        return;

      // find each span that looks like a token and look it up in the dictionary
      foreach (var curTag in _aggregator.GetTags(new SnapshotSpan(triggerPoint, triggerPoint)))
      {
        if (curTag.Tag.type == Tokens.SYMBOL)
        {
          SnapshotSpan tagSpan = curTag.Span.GetSpans(_buffer).First();

          var text = tagSpan.GetText();

          if (_buffer.Properties.ContainsProperty("SchemeEnvironment"))
          {
            var env = _buffer.Properties["SchemeEnvironment"];
            var bindings = _buffer.Properties["SchemeBindings"] as Dictionary<string, BindingType>;

            BindingType type;

            if (bindings.TryGetValue(text, out type) && type == BindingType.Procedure)
            {
              try
              {
                var proc = ("(eval '" + text + " {0})").Eval(env);
                var forms = "(get-forms {0})".Eval<string>(proc).Trim();

                applicableToSpan = _buffer.CurrentSnapshot.CreateTrackingSpan(tagSpan, SpanTrackingMode.EdgeExclusive);
                quickInfoContent.Add(forms);
              }
              catch (SchemeException ex)
              {
              }
            }
          }
        }
      }
    }

    public void Dispose()
    {
    }
  }
}
