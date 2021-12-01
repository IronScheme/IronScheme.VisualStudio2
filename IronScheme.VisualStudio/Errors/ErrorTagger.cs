using System;
using System.Collections.Generic;
using System.Linq;
using IronScheme.Compiler;
using IronScheme.Runtime;
using IronScheme.Scripting;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace IronScheme.VisualStudio.Errors
{
  class SnapshotSpanPair
  {
    public SnapshotSpan Start { get; set; }
    public SnapshotSpan End { get; set; }

    public readonly static object BraceKey = new object();
  }

  class Square : SnapshotSpanPair { }

  class ErrorTagger : BaseTagger, ITagger<ErrorTag>, IDisposable
  {
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;
    ITextDocument _document;
    ITextView _view;
    private readonly ITableDataSink tableDataSink;
    readonly List<TagSpan<ErrorTag>> brace_errors = new List<TagSpan<ErrorTag>>();
    TagSpan<ErrorTag> syntax_error;
    ErrorsSnapshot _snapshot = null;

    public ErrorTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory,
      ITextDocumentFactoryService textDocumentFactory, ITextView view, ITableDataSink tableDataSink)
    {
      _buffer = buffer;
      _view = view;
      this.tableDataSink = tableDataSink;
      _aggregator = aggregatorFactory.CreateTagAggregator<SchemeTag>(buffer);

      textDocumentFactory.TryGetTextDocument(_buffer, out _document);

      _snapshot = new ErrorsSnapshot(_document.FilePath, 1);

      BufferIdleEventUtil.AddBufferIdleEventListener(_buffer, ReparseFile);
    }

    public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      foreach (var tagSpan in _aggregator.GetTags(spans))
      {
        if (tagSpan.Tag.type == Tokens.error)
        {
          var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)[0];
          var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, tagSpan.Tag.ErrorMessage);
          yield return new TagSpan<ErrorTag>(tagSpans, errtag);
        }
      }

      if (syntax_error != null)
      {
        if (syntax_error.Span.Snapshot != spans[0].Snapshot)
        {
          syntax_error = null;
        }
        else if (syntax_error.Span.OverlapsWith(spans[0]))
        {
          yield return syntax_error;
        }
      }

      foreach (var err in brace_errors)
      {
        if (err.Span.Snapshot != spans[0].Snapshot)
        {
          brace_errors.Clear();
          break;
        }
        else if (err.Span.OverlapsWith(spans[0]))
        {
          yield return err;
        }
      }
    }

    public override void Dispose()
    {
      BufferIdleEventUtil.RemoveBufferIdleEventListener(_buffer, ReparseFile);
    }

    void ReparseFile(object sender, EventArgs args)
    {
      var snapshot = _buffer.CurrentSnapshot;
      var spans = new NormalizedSnapshotSpanCollection(new SnapshotSpan(snapshot, 0, snapshot.Length));

      _snapshot.Errors.Clear();
      brace_errors.Clear();
      syntax_error = null;

      var bracestack = new Stack<SnapshotSpanPair>();
      var bracelist = new List<SnapshotSpanPair>();

      foreach (var tagSpan in _aggregator.GetTags(spans))
      {
        var span = tagSpan.Span.GetSpans(spans[0].Snapshot)[0];

        switch (tagSpan.Tag.type)
        {
          case Tokens.error:
            {
              var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, tagSpan.Tag.ErrorMessage);
              AddErrorTask(span, errtag);
              break;
            }
          case Tokens.LBRACK:
            {
              var tup = new Square { Start = span };
              bracestack.Push(tup);
              bracelist.Add(tup);
              break;
            }
          case Tokens.LBRACE:
          case Tokens.VALUEVECTORLBRACE:
          case Tokens.VECTORLBRACE:
            {
              var tup = new SnapshotSpanPair { Start = span };
              bracestack.Push(tup);
              bracelist.Add(tup);
              break;
            }
          case Tokens.RBRACE:
          case Tokens.RBRACK:
            {
              if (bracestack.Count > 0)
              {
                var tup = bracestack.Pop();
                if (tagSpan.Tag.type == Tokens.RBRACK && !(tup is Square) || tagSpan.Tag.type == Tokens.RBRACE && tup is Square)
                {
                  var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "Missing opening parenthesis");
                  AddErrorTask(span, errtag);
                  brace_errors.Add(new TagSpan<ErrorTag>(span, errtag));
                }
                else
                {
                  tup.End = span;
                }
              }
              else
              {
                var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "Missing opening parenthesis");
                AddErrorTask(span, errtag);
                brace_errors.Add(new TagSpan<ErrorTag>(span, errtag));
              }
              break;
            }
        }
      }

      while (bracestack.Count > 0)
      {
        var bs = bracestack.Pop();
        var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "Missing closing parenthesis");
        AddErrorTask(bs.Start, errtag);
        brace_errors.Add(new TagSpan<ErrorTag>(bs.Start, errtag));
      }

      _buffer.Properties[SnapshotSpanPair.BraceKey] = bracelist;

      if (_snapshot.Errors.Count == 0)
      {
        var port = new TextSnapshotToTextReader(snapshot);

        _buffer.Properties.TryGetProperty("SchemeBindings", out object prevbindings);
        {
          try
          {
            var result = "(read-file {0})".Eval(port);

            if (result == null)
            {
              return;
            }

            _buffer.Properties["Result"] = result;

            var imports = "(read-imports {0})".Eval(result);
            var env = "(apply environment {0})".Eval(imports);

            _buffer.Properties["SchemeEnvironment"] = env;

            var b = "(environment-bindings {0})".Eval(env);

            var bindings = ((Cons)b).ToDictionary(x => ((Cons)x).car.ToString(), GetBindingType);

            _buffer.Properties["SchemeBindings"] = bindings;

            var expanded = "(run-expansion {0})".Eval<MultipleValues>(result).ToArray(3);
            var names = expanded[0] as object[];
            var types = expanded[1] as object[];
            var output = expanded[2];

            for (var i = 0; i < names.Length; i++)
            {
              if (names[i] is SymbolId)
              {
                var name = SymbolTable.IdToString((SymbolId)names[i]);
                // ignore lst
                int foo;
                if (name == "using" || name == "dummy" || int.TryParse(name, out foo))
                {
                  continue;
                }
                bindings[name] = GetBindingType2(types[i]) | BindingType.LocalMask;
              }
            }
          }
          catch (SchemeException ex)
          {
            _buffer.Properties["SchemeBindings"] = prevbindings;
            var cond = ex.Condition;
            var error = "(get-error {0})".Eval<MultipleValues>(cond).ToArray(2);
            var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, error[0]);
            var errspan = new SnapshotSpan(snapshot, 0, 0);
            if (error[1] is string)
            {
              errspan = MakeSnapshotSpan(snapshot, error[1] as string) ?? errspan;
              syntax_error = new TagSpan<ErrorTag>(errspan, errtag);
            }
            AddErrorTask(errspan, errtag);

          }
          catch (Exception ex)
          {
            _buffer.Properties["SchemeBindings"] = prevbindings;
            var errtag = new ErrorTag(PredefinedErrorTypeNames.CompilerError, ex.ToString());
            var errspan = new SnapshotSpan(snapshot, 0, 0);

            AddErrorTask(errspan, errtag);
          }

          var lines = _view.TextViewLines.ToArray();

          var start = lines[0].Start;
          var end = lines[lines.Length - 1].End;

          var span = new SnapshotSpan(start, end);

          // notifiy classifier
          if (_buffer.Properties.TryGetProperty<ClassificationTagger>(typeof(ITagger<IClassificationTag>), out var classifier))
          {
            classifier.RaiseTagsChanged(span);
          }

          if (_buffer.Properties.TryGetProperty<StructureTagger>(typeof(ITagger<IStructureTag>), out var structure))
          {
            structure.RaiseTagsChanged(span);
          }

          if (_buffer.Properties.TryGetProperty<OutlineTagger>(typeof(ITagger<IOutliningRegionTag>), out var outline))
          {
            outline.RaiseTagsChanged(span);
          }

        }
      }

      tableDataSink.AddSnapshot(_snapshot, true);

      RaiseTagsChanged(new SnapshotSpan(snapshot, 0, snapshot.Length));
    }

    static BindingType GetBindingType(object x)
    {
      var type = SymbolTable.IdToString((SymbolId)((Cons)x).cdr);

      switch (type)
      {
        case "syntax": return BindingType.Syntax;
        case "procedure": return BindingType.Procedure;
        case "record": return BindingType.Record;
        default: return BindingType.Unknown;
      }
    }

    static BindingType GetBindingType2(object x)
    {
      var type = SymbolTable.IdToString((SymbolId)x);

      switch (type)
      {
        case "global-macro": return BindingType.Syntax;
        case "global": return BindingType.Procedure;
        case "$rtd": return BindingType.Record;
        default: return BindingType.Unknown;
      }
    }

    void AddErrorTask(SnapshotSpan span, ErrorTag tag)
    {
      _snapshot.Errors.Add(new TagSpan<ErrorTag>(span, tag));

    }
  }
}
