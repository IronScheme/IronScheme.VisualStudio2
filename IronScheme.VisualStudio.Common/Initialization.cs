using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IronScheme.Runtime;

namespace IronScheme.VisualStudio.Common
{
  public static class Initialization
  {
    public static readonly bool Complete = Initialize();

    static bool Initialize()
    {
      "(library-path (list {0}))".Eval(Builtins.ApplicationDirectory);

      var cfgpath = Path.Combine(Builtins.ApplicationDirectory, "../config.ss");

      if (File.Exists(cfgpath))
      {
        string.Format("(include \"{0}\")", cfgpath.Replace('\\', '/')).Eval();
      }

      "(import (visualstudio))".Eval();
      return true;
    }
  }
}
