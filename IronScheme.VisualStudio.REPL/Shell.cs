using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using IronScheme.Runtime;
using IronScheme.Scripting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace IronScheme.VisualStudio
{
    public static class Shell
    {
        public static IEditorOperationsFactoryService EditorOperationsFactoryService { get; internal set; }
        internal static Dictionary<string, ITextView> Views { get; } = new Dictionary<string, ITextView>();

        public static Cons OpenViews() => Cons.FromList(Views.Keys);

        public static void SetView(string filename)
        {
            if (Views.TryGetValue(filename, out var view))
            {
                var ops = EditorOperationsFactoryService.GetEditorOperations(view);
                if (ops != null)
                {
                    "(view {0})".Eval(ops);
                }
            }
        }

        public static Cons GetOperations()
        {
            var methods = new List<Cons>();

            var all = Methods<IEditorOperations>().Concat(Methods<IEditorOperations2>()).Concat(Methods<IEditorOperations3>());

            foreach (var meth in all)
            {
                if (CanDealWithParams(meth))
                {
                    var clrType = SymbolTable.StringToId(meth.DeclaringType.Name);
                    var clrName = SymbolTable.StringToId(meth.Name);
                    var name = Schemify(meth.Name.Replace("get_", ""));
                    var args = Cons.FromList(meth.GetParameters().Select(x => Schemify(x.Name)));
                    methods.Add(new Cons(clrType, new Cons(clrName, new Cons(name, args))));
                }
            }

            return Cons.FromList(methods);

            MethodInfo[] Methods<T>()
            {
                return typeof(T).GetMethods();
            }
        }

        private static object Schemify(string name)
        {
            var newname = Regex.Replace(name, "[A-Z]", m => m.Index == 0 ? m.Value.ToLower() : ("-" + m.Value.ToLower()));
            return SymbolTable.StringToId(newname);
        }


        static HashSet<Type> safetypes = new HashSet<Type>()
        { 
            typeof(string),
            typeof(bool),
            typeof(int),
            typeof(void)
        };

        private static bool CanDealWithParams(System.Reflection.MethodInfo meth)
        {
            if (safetypes.Contains(meth.ReturnType))
            {
                var pars = meth.GetParameters();
                return pars.Length == 0 || pars.All(p => safetypes.Contains(p.ParameterType));
            }

            return false;
        }
    }
}
