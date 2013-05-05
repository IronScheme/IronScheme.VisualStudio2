using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IronScheme.Runtime;
using System.Windows.Forms;

namespace IronScheme.VisualStudio.Common
{
  public static class Initialization
  {
    public static readonly bool Complete = Initialize();

    static bool Initialize()
    {
      "(library-path (list {0}))".Eval(Builtins.ApplicationDirectory);

      var cfgpath = Path.Combine(Builtins.ApplicationDirectory, "../config.ss");

      if (!File.Exists(cfgpath))
      {
        MessageBox.Show(new FileInfo(cfgpath).FullName, "Missing config.ss");
        var bfd = new FolderBrowserDialog();
        if (bfd.ShowDialog() == DialogResult.OK)
        {
          File.WriteAllText(cfgpath, string.Format(@"
(library-path 
  (append (library-path) 
          (list ""{0}"")))", bfd.SelectedPath.Replace("\\", "/")));
        }
      }

      if (File.Exists(cfgpath))
      {
        string.Format("(include \"{0}\")", cfgpath.Replace('\\', '/')).Eval();
      }

      "(import (visualstudio))".Eval();
      return true;
    }
  }
}
