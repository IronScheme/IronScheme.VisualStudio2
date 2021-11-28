using System.Collections.Generic;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Text.Tagging;

namespace IronScheme.VisualStudio.Errors
{
  class ErrorsSnapshot : WpfTableEntriesSnapshotBase
  {
    private readonly string _filePath;
    private readonly int _versionNumber;

    // We're not using an immutable list here but we cannot modify the list in any way once we've published the snapshot.
    public readonly List<TagSpan<ErrorTag>> Errors = new List<TagSpan<ErrorTag>>();

    //public ErrorsSnapshot NextSnapshot;

    internal ErrorsSnapshot(string filePath, int versionNumber)
    {
      _filePath = filePath;
      _versionNumber = versionNumber;
    }

    public override int Count
    {
      get
      {
        return Errors.Count;
      }
    }

    public override int VersionNumber
    {
      get
      {
        return _versionNumber;
      }
    }

    public override bool TryGetValue(int index, string columnName, out object content)
    {
      if (index >= 0 && index < Errors.Count)
      {
        if (columnName == StandardTableKeyNames.DocumentName)
        {
          // We return the full file path here. The UI handles displaying only the Path.GetFileName().
          content = _filePath;
          return true;
        }
        else if (columnName == StandardTableKeyNames.ErrorCategory)
        {
          content = "Documentation";
          return true;
        }
        else if (columnName == StandardTableKeyNames.ErrorSource)
        {
          content = "IronScheme";
          return true;
        }
        else if (columnName == StandardTableKeyNames.Line)
        {
          // Line and column numbers are 0-based (the UI that displays the line/column number will add one to the value returned here).
          content = Errors[index].Span.Start.GetContainingLine().LineNumber;

          return true;
        }
        else if (columnName == StandardTableKeyNames.Column)
        {
          var position = Errors[index].Span.Start;
          var line = position.GetContainingLine();
          content = position.Position - line.Start.Position;

          return true;
        }
        else if (columnName == StandardTableKeyNames.Text)
        {
          content = Errors[index].Tag.ToolTipContent + ": " + Errors[index].Span.GetText();

          return true;
        }
        else if (columnName == StandardTableKeyNames2.TextInlines)
        {

          //var inlines = new List<Inline>();

          //inlines.Add(new Run("Spelling: "));
          //inlines.Add(new Run(this.Errors[index].Span.GetText())
          //{
          //  FontWeight = FontWeights.ExtraBold
          //});

          //content = inlines;

          //return true;
        }
        else if (columnName == StandardTableKeyNames.ErrorSeverity)
        {
          content = __VSERRORCATEGORY.EC_ERROR;

          return true;
        }
        else if (columnName == StandardTableKeyNames.ErrorSource)
        {
          content = ErrorSource.Other;

          return true;
        }
        else if (columnName == StandardTableKeyNames.BuildTool)
        {
          content = "IronScheme";

          return true;
        }
        else if (columnName == StandardTableKeyNames.ErrorCode)
        {
          content = Errors[index].Tag.ErrorType;

          return true;
        }

        // We should also be providing values for StandardTableKeyNames.Project & StandardTableKeyNames.ProjectName but that is
        // beyond the scope of this sample.
      }

      content = null;
      return false;
    }
  }
}
