using System;
using System.Linq;
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
using Microsoft.Scripting;
using System.IO;

namespace IronScheme.VisualStudio
{
  [Export(typeof(ITaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(ClassificationTag))]
  internal class ClassificationTaggerProvider : ITaggerProvider
  {
    [Export]
    [Name("scheme")]
    [DisplayName("scheme")]
    [BaseDefinition("code")]
    public static ContentTypeDefinition SchemeContentType = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".sls")]
    public static FileExtensionToContentTypeDefinition SlsFileExtension = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".sps")]
    public static FileExtensionToContentTypeDefinition SpsFileExtension = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".ss")]
    public static FileExtensionToContentTypeDefinition SsFileExtension = null;

    [Export]
    [ContentType("scheme")]
    [FileExtension(".scm")]
    public static FileExtensionToContentTypeDefinition ScmFileExtension = null;

    [Import]
    internal IClassificationTypeRegistryService ClassificationRegistry = null; // Set via MEF
    [Import]
    internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

    public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        delegate { return new ClassificationTagger(buffer, aggregatorFactory, ClassificationRegistry) as ITagger<T>; });
    }
  }

  class ClassificationTagger : ITagger<ClassificationTag>
  {
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;
    IDictionary<Tokens, IClassificationType> _schemeTokenTypes = new Dictionary<Tokens, IClassificationType>();
    IClassificationType syntax, procedure;
    static readonly Dictionary<string, bool> bindings = new Dictionary<string, bool>(1000);
    static readonly HashSet<string> library_keywords = new HashSet<string>(new string[] { "library", "export", "import", "only", "except", "rename", "prefix" });

    static ClassificationTagger()
    {
      const string FILENAME = "visualstudio.sls";

      //if (!File.Exists(Path.Combine(Builtins.ApplicationDirectory, FILENAME)))
      {
        var stream = typeof(ErrorTagger).Assembly.GetManifestResourceStream("IronScheme.VisualStudio." + FILENAME);
        using (var file = File.Create(Path.Combine(Builtins.ApplicationDirectory, FILENAME)))
        {
          stream.CopyTo(file);
        }
      }

      var s = SymbolTable.StringToObject("syntax");
      var p = SymbolTable.StringToObject("procedure");
      var b = "(environment-bindings (environment '(ironscheme)))".Eval();
      bindings = ((Cons)b).Where(x => ((Cons)x).cdr == s || ((Cons)x).cdr == p).ToDictionary(x => (((Cons)x).car).ToString(), x => ((Cons)x).cdr == s);

      //"(library-path (list {0} {1}))".Eval(Builtins.ApplicationDirectory, @"d:\dev\IronScheme\IronScheme\IronScheme.Console\bin\Release\");
      "(library-path (list {0} {1}))".Eval(Builtins.ApplicationDirectory, @"c:\dev\IronScheme\IronScheme.Console\bin\Release\");
      "(import (visualstudio))".Eval();
    }

    internal ClassificationTagger(ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory, IClassificationTypeRegistryService registry)
    {
      _buffer = buffer;
      _aggregator = aggregatorFactory.CreateTagAggregator<SchemeTag>(buffer);

      _schemeTokenTypes[Tokens.COMMENT] = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
      _schemeTokenTypes[Tokens.CHARACTER] = registry.GetClassificationType(PredefinedClassificationTypeNames.Character);
      _schemeTokenTypes[Tokens.DIRECTIVE] = registry.GetClassificationType(PredefinedClassificationTypeNames.PreprocessorKeyword);
      _schemeTokenTypes[Tokens.LITERAL] = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
      _schemeTokenTypes[Tokens.NUMBER] = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
      _schemeTokenTypes[Tokens.STRING] = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
      _schemeTokenTypes[Tokens.SYMBOL] = registry.GetClassificationType(PredefinedClassificationTypeNames.Identifier);
      _schemeTokenTypes[Tokens.MLSTRING] = registry.GetClassificationType(PredefinedClassificationTypeNames.String);

      syntax = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
      procedure = registry.GetClassificationType("line number");

      _buffer.Properties["SchemeBindings"] = bindings;

      _aggregator.BatchedTagsChanged += _aggregator_BatchedTagsChanged;

      _buffer.Properties["SchemeClassifier"] = this;
    }

    internal void RaiseTagsChanged(SnapshotSpan span)
    {
      var e = new SnapshotSpanEventArgs(span);
      if (TagsChanged != null)
      {
        TagsChanged(this, e);
      }
    }

    void _aggregator_BatchedTagsChanged(object sender, BatchedTagsChangedEventArgs e)
    {
      foreach (var span in e.Spans)
      {
        RaiseTagsChanged(span.GetSpans(_buffer)[0]);
      }
    }

    public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
    {
      var bindings = _buffer.Properties["SchemeBindings"] as Dictionary<string, bool>;
      foreach (var tagSpan in this._aggregator.GetTags(spans))
      {
        if (_schemeTokenTypes.ContainsKey(tagSpan.Tag.type))
        {
          var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)[0];
          if (tagSpan.Tag.type == Tokens.SYMBOL)
          {
            var text = tagSpans.GetText();
            bool val;
            if (bindings.TryGetValue(text, out val))
            {
              if (val)
              {
                yield return new TagSpan<ClassificationTag>(tagSpans, new ClassificationTag(syntax));
              }
              else
              {
                yield return new TagSpan<ClassificationTag>(tagSpans, new ClassificationTag(procedure));
              }
              continue;
            }
            if (library_keywords.Contains(text))
            {
              yield return new TagSpan<ClassificationTag>(tagSpans, new ClassificationTag(syntax));
              continue;
            }
          }
          
          yield return new TagSpan<ClassificationTag>(tagSpans, new ClassificationTag(_schemeTokenTypes[tagSpan.Tag.type]));
        }
      }
    }

    public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
  }
}
