using System;
using System.Text.RegularExpressions;
using IronScheme.Scripting;
using Microsoft.VisualStudio.Text;

namespace IronScheme.VisualStudio
{
    internal class BaseTagger : IDisposable
    {
        static readonly Regex LOCATIONMATCH = new Regex(
    @"\((?<startline>\d+),(?<startcol>\d+)\)\s-\s\((?<endline>\d+),(?<endcol>\d+)\)",
    RegexOptions.Compiled);

        static SourceSpan ExtractLocation(string location)
        {
            var m = LOCATIONMATCH.Match(location);

            return new SourceSpan(
              new SourceLocation(0, Convert.ToInt32(m.Groups["startline"].Value), Convert.ToInt32(m.Groups["startcol"].Value)),
              new SourceLocation(0, Convert.ToInt32(m.Groups["endline"].Value), Convert.ToInt32(m.Groups["endcol"].Value)));
        }

        protected static SnapshotSpan? MakeSnapshotSpan(ITextSnapshot snapshot, string location)
        {
            try
            {
                var loc = ExtractLocation(location);

                var start = snapshot.GetLineFromLineNumber(loc.Start.Line - 1).Start + (loc.Start.Column - 1);
                var end = snapshot.GetLineFromLineNumber(loc.End.Line - 1).Start + (loc.End.Column - 1);

                return new SnapshotSpan(start, end);
            }
            catch
            {
                return null;
            }
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        internal void RaiseTagsChanged(SnapshotSpan span)
        {
            var e = new SnapshotSpanEventArgs(span);
            if (TagsChanged != null)
            {
                TagsChanged(this, e);
            }
        }

        public virtual void Dispose()
        {

        }
    }
}
