using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(ErrorTag))]
  class ErrorTaggerProvider : ITaggerProvider
  {
    [Import]
    internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

    [Import(typeof(SVsServiceProvider))]
    internal IServiceProvider _serviceProvider = null;

    [Import]
    internal ITextDocumentFactoryService textDocumentFactory = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        () => new ErrorTagger(buffer, aggregatorFactory, _serviceProvider, textDocumentFactory) as ITagger<T>);
    }
  }

  class SnapshotSpanPair
  {
    public SnapshotSpan Start { get; set; }
    public SnapshotSpan End { get; set; }

    public readonly static object BraceKey = new object();
  }

  class ErrorTagger : ITagger<ErrorTag>, IDisposable
  {
    const string FILENAME = "visualstudio.sls";
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;
    ErrorListProvider _errorProvider;
    ITextDocument _document;

    public ErrorTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory,
      IServiceProvider svcp, ITextDocumentFactoryService textDocumentFactory)
    {
      _buffer = buffer;
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

      "(library-path (list {0} {1}))".Eval(Builtins.ApplicationDirectory, @"d:\dev\IronScheme\IronScheme\IronScheme.Console\bin\Release\");
      "(import (visualstudio))".Eval();


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

      var bracestack = new Stack<SnapshotSpanPair>();
      var bracelist = new List<SnapshotSpanPair>();

      foreach (var tagSpan in this._aggregator.GetTags(spans))
      {
        var span = tagSpan.Span.GetSpans(spans[0].Snapshot)[0];

        switch (tagSpan.Tag.type)
        {
          case Tokens.error:
            var errtag = new ErrorTag(PredefinedErrorTypeNames.SyntaxError, tagSpan.Tag.ErrorMessage);
            AddErrorTask(span, errtag);
            break;
          case Tokens.LBRACE:
          case Tokens.LBRACK:
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
                tup.End = span;
              }
              break;
            }
        }
      }

      // maybe add check for balanced braces?

      _buffer.Properties[SnapshotSpanPair.BraceKey] = bracelist;

      var port = new TextSnapshotToTextReader(snapshot);

      var result = "(read-file {0})".Eval(port);
      var imports = "(read-imports {0})".Eval(result);
      var env = "(environment {0})".Eval(imports);
      var symbols = "(environment-bindings {0})".Eval(env);

      Console.WriteLine(symbols);


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
