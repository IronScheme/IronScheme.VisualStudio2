using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using IronScheme.Compiler;
using IronScheme.Runtime;
using Microsoft.Scripting;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio
{
  [Export(typeof(IViewTaggerProvider))]
  [ContentType("scheme")]
  [TagType(typeof(ClassificationTag))]
  internal class ClassificationTaggerProvider : IViewTaggerProvider
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
    internal IClassificationTypeRegistryService ClassificationRegistry = null; 
    [Import]
    internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

    public ITagger<T> CreateTagger<T>(ITextView view, ITextBuffer buffer) where T : ITag
    {
      return buffer.Properties.GetOrCreateSingletonProperty(
        delegate { return new ClassificationTagger(view, buffer, aggregatorFactory, ClassificationRegistry) as ITagger<T>; });
    }
  }

  enum BindingType
  {
    Syntax,
    Procedure,
    Record,
    Unknown
  }

  class ClassificationTagger : ITagger<ClassificationTag>
  {
    ITagAggregator<SchemeTag> _aggregator;
    ITextBuffer _buffer;
    IDictionary<Tokens, IClassificationType> _schemeTokenTypes = new Dictionary<Tokens, IClassificationType>();
    IClassificationType syntax, procedure, record;
    static readonly Dictionary<string, BindingType> bindings = new Dictionary<string, BindingType>(1000);
    static readonly HashSet<string> library_keywords = new HashSet<string>(new string[] { "library", "export", "import", "only", "except", "rename", "prefix" });

    static ClassificationTagger()
    {
      var s = SymbolTable.StringToObject("syntax");
      var p = SymbolTable.StringToObject("procedure");
      var b = "(environment-bindings (environment '(ironscheme)))".Eval();
      bindings = ((Cons)b).ToDictionary(x => (((Cons)x).car).ToString(), GetBindingType);

      "(library-path (list {0}))".Eval(Builtins.ApplicationDirectory);

      var cfgpath = Path.Combine(Builtins.ApplicationDirectory, "../config.ss");

      if (File.Exists(cfgpath))
      {
        string.Format("(include \"{0}\")", cfgpath.Replace('\\', '/')).Eval();
      }

      "(import (visualstudio))".Eval();
    }

    static BindingType GetBindingType(object x)
    {
      var type = SymbolTable.IdToString((SymbolId) ((Cons)x).cdr);

      switch (type)
      {
        case "syntax": return BindingType.Syntax;
        case "procedure": return BindingType.Procedure;
        case "record": return BindingType.Record;
        default: return BindingType.Unknown;
      }

    }

    internal ClassificationTagger(ITextView view, ITextBuffer buffer, IBufferTagAggregatorFactoryService aggregatorFactory, IClassificationTypeRegistryService registry)
    {
      _buffer = buffer;

      var options = view.Options;

      options.SetOptionValue(DefaultOptions.ConvertTabsToSpacesOptionId, true);
      options.SetOptionValue(DefaultOptions.IndentSizeOptionId, 2);
      options.SetOptionValue(DefaultOptions.TabSizeOptionId, 2);
      options.SetOptionValue(DefaultTextViewHostOptions.LineNumberMarginId, true);

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
      record = registry.GetClassificationType("cppmacro");

      _buffer.Properties["SchemeBindings"] = bindings;

      _aggregator.BatchedTagsChanged += _aggregator_BatchedTagsChanged;
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
      var bindings = _buffer.Properties["SchemeBindings"] as Dictionary<string, BindingType>;
      foreach (var tagSpan in this._aggregator.GetTags(spans))
      {
        if (_schemeTokenTypes.ContainsKey(tagSpan.Tag.type))
        {
          var tagSpans = tagSpan.Span.GetSpans(spans[0].Snapshot)[0];
          if (tagSpan.Tag.type == Tokens.SYMBOL)
          {
            var text = tagSpans.GetText();
            BindingType val;
            if (bindings.TryGetValue(text, out val))
            {
              switch (val)
              {
                case BindingType.Syntax:
                  yield return new TagSpan<ClassificationTag>(tagSpans, new ClassificationTag(syntax));
                  continue;
                case BindingType.Procedure:
                  yield return new TagSpan<ClassificationTag>(tagSpans, new ClassificationTag(procedure));
                  continue;
                case BindingType.Record:
                  yield return new TagSpan<ClassificationTag>(tagSpans, new ClassificationTag(record));
                  continue;
              }
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
