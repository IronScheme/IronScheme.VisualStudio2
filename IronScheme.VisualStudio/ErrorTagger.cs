using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Media;
using IronScheme.Compiler;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Language.StandardClassification;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Shell;
using IronScheme.Runtime;
using System.IO;
using Microsoft.Scripting;
using Microsoft.VisualStudio.Text.Editor;

namespace IronScheme.VisualStudio
{
  [Export(typeof(IViewTaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(ErrorTag))]
  class ErrorTaggerProvider : IViewTaggerProvider
  {
    [Import]
    internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider _serviceProvider = null;

    [Import]
    internal ITextDocumentFactoryService textDocumentFactory = null;

    public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        () => new ErrorTagger(buffer, aggregatorFactory, _serviceProvider, textDocumentFactory, textView) as ITagger<T>);
    }
  }

  class SnapshotSpanPair
  {
    public SnapshotSpan Start { get; set; }
    public SnapshotSpan End { get; set; }

    public readonly static object BraceKey = new object();
  }

  class Square : SnapshotSpanPair { }

  class ErrorTagger : ITagger<ErrorTag>, IDisposable
  {
    const string FILENAME = "visualstudio.sls";
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;
    ErrorListProvider _errorProvider;
    ITextDocument _document;
    ITextView _view;
    readonly List<TagSpan<ErrorTag>> brace_errors = new List<TagSpan<ErrorTag>>();

    public ErrorTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory,
      IServiceProvider svcp, ITextDocumentFactoryService textDocumentFactory, ITextView view)
    {
      _buffer = buffer;
      _view = view;
      _aggregator = aggregatorFactory.CreateTagAggregator<SchemeTag>(buffer);

      textDocumentFactory.TryGetTextDocument(_buffer, out _document);

      _errorProvider = new ErrorListProvider(svcp);

      //if (!File.Exists(Path.Combine(Builtins.ApplicationDirectory, FILENAME)))
      {
        var s = typeof (ErrorTagger).Assembly.GetManifestResourceStream("IronScheme.VisualStudio." + FILENAME);
        using (var file = File.Create(Path.Combine(Builtins.ApplicationDirectory, FILENAME)))
        {
          s.CopyTo(file);
        }
      }

      BufferIdleEventUtil.AddBufferIdleEventListener(_buffer, ReparseFile);
    }

    public IEnumerable<ITagSpan<ErrorTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      foreach (var tagSpan in this._aggregator.GetTags(spans))
      {
        if (tagSpan.Tag.type == Tokens.error)
        {
          var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)[0];
          var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, tagSpan.Tag.ErrorMessage);
          yield return new TagSpan<ErrorTag>(tagSpans, errtag);
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
          brace_errors.Remove(err);
          break;
        }
      }
    }

    public void Dispose()
    {
      if (_errorProvider != null)
      {
        _errorProvider.Tasks.Clear();
        _errorProvider.Dispose();
      }

      BufferIdleEventUtil.RemoveBufferIdleEventListener(_buffer, ReparseFile);
    }

    void ReparseFile(object sender, EventArgs args)
    {
      ITextSnapshot snapshot = _buffer.CurrentSnapshot;
      var spans = new NormalizedSnapshotSpanCollection(new SnapshotSpan(snapshot, 0, snapshot.Length));

      _errorProvider.Tasks.Clear();

      brace_errors.Clear();

      var bracestack = new Stack<SnapshotSpanPair>();
      var bracelist = new List<SnapshotSpanPair>();

      foreach (var tagSpan in this._aggregator.GetTags(spans))
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
          case Tokens.BYTEVECTORLBRACE:
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
                if ((tagSpan.Tag.type == Tokens.RBRACK && !(tup is Square)) || (tagSpan.Tag.type == Tokens.RBRACE && (tup is Square)))
                {
                  //bracestack.Push(tup);
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

      // maybe add check for balanced braces?

      while (bracestack.Count > 0)
      {
        var bs = bracestack.Pop();
        var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, "Missing closing parenthesis");
        AddErrorTask(bs.Start, errtag);
        brace_errors.Add(new TagSpan<ErrorTag>(bs.Start, errtag));
      }

      _buffer.Properties[SnapshotSpanPair.BraceKey] = bracelist;

      if (_errorProvider.Tasks.Count == 0)
      {
        var port = new TextSnapshotToTextReader(snapshot);

        var result = "(read-file {0})".Eval(port);
        var imports = "(read-imports {0})".Eval(result);
        var env = "(apply environment {0})".Eval(imports);
        var b = "(environment-bindings {0})".Eval(env);

        var s = SymbolTable.StringToObject("syntax");
        var bindings = ((Cons)b).ToDictionary(x => (((Cons)x).car).ToString(), x => ((Cons)x).cdr == s);

        _buffer.Properties["SchemeBindings"] = bindings;

        var lines = _view.TextViewLines.ToArray();
        
        var start = lines[0].Start;
        var end = lines[lines.Length - 1].End;

        var span = new SnapshotSpan(start, end);
        
        // notifiy classifier somehow
        var classifier = _buffer.Properties["SchemeClassifier"] as ClassificationTagger;
        classifier.RaiseTagsChanged(span);
      }

      if (TagsChanged != null)
      {
        TagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
      }

    }


    void AddErrorTask(SnapshotSpan span, ErrorTag tag)
    {
      if (_errorProvider != null)
      {
        var task = new ErrorTask();
        task.CanDelete = true;
        if (_document != null)
          task.Document = _document.FilePath;
        task.ErrorCategory = TaskErrorCategory.Error;
        task.Text = tag.ToolTipContent as string;
        task.Line = span.Start.GetContainingLine().LineNumber;
        task.Column = span.Start.Position - span.Start.GetContainingLine().Start.Position;

        task.Navigate += task_Navigate;

        _errorProvider.Tasks.Add(task);
      }
    }

    void task_Navigate(object sender, EventArgs e)
    {
      ErrorTask error = sender as ErrorTask;

      if (error != null)
      {
        error.Line += 1;
        error.Column += 1;
        _errorProvider.Navigate(error, new Guid(EnvDTE.Constants.vsViewKindCode));
        error.Column -= 1;
        error.Line -= 1;
      }
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
  }
}
