using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace IronScheme.VisualStudio.Errors
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("scheme")]
    [TagType(typeof(ErrorTag))]
    class ErrorTaggerProvider : IViewTaggerProvider, ITableDataSource, IDisposable
    {
        public ErrorTaggerProvider()
        {
            var b = ClassificationTagger.bindings;
            if (b is null)
            {
                throw new InvalidOperationException("binding is null");
            }
        }

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

        [Import]
        internal ITextDocumentFactoryService textDocumentFactory = null;

        [Import]
        internal ITableManagerProvider tableManagerProvider = null;

        public string SourceTypeIdentifier => StandardTableDataSources.ErrorTableDataSource;

        public string Identifier => "IronScheme";

        public string DisplayName => "IronScheme";

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            var errorTableManager = tableManagerProvider.GetTableManager(StandardTables.ErrorsTable);
            errorTableManager.AddSource(this, StandardTableColumnDefinitions.DetailsExpander,
                                              StandardTableColumnDefinitions.ErrorSeverity, StandardTableColumnDefinitions.ErrorCode,
                                              StandardTableColumnDefinitions.ErrorSource, StandardTableColumnDefinitions.BuildTool,
                                              StandardTableColumnDefinitions.ErrorCategory, StandardTableColumnDefinitions.Text,
                                              StandardTableColumnDefinitions.DocumentName,
                                              StandardTableColumnDefinitions.Line, StandardTableColumnDefinitions.Column);

            return buffer.Properties.GetOrCreateSingletonProperty(
              () => new ErrorTagger(buffer, aggregatorFactory, textDocumentFactory, textView, _sink) as ITagger<T>);
        }

        ITableDataSink _sink;

        public IDisposable Subscribe(ITableDataSink sink)
        {
            _sink = sink;
            return this;
        }

        public void Dispose()
        {
            _sink = null;
        }
    }
}
