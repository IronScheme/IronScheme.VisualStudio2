using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace IronScheme.VisualStudio
{
  internal static class TextBufferExtensions
  {
    public static bool TryGetBindings(this ITextBuffer _buffer, out Dictionary<string, BindingType> bindings)
    {
      return _buffer.Properties.TryGetProperty("SchemeBindings", out bindings);
    }

    public static bool TryGetEnvironment(this ITextBuffer _buffer, out object env)
    {
      return _buffer.Properties.TryGetProperty("SchemeEnvironment", out env);
    }

    public static bool TryGetResult(this ITextBuffer _buffer, out object result)
    {
      return _buffer.Properties.TryGetProperty("Result", out result);
    }
  }
}
