// Guids.cs
// MUST match guids.h
using System;
using System.Diagnostics.CodeAnalysis;

namespace IronScheme.VisualStudio.REPL
{
    public static class ConsoleGuidList
    {
      public const string guidIronSchemeConsolePkgString = "160FCEF2-DED9-4DC2-8E42-985446204ACE";
      public const string guidIronSchemeConsoleCmdSetString = "9A3D771E-AA64-4098-AD87-BADA43A16F24";
      public static readonly Guid guidIronSchemeConsolePkg = new Guid(guidIronSchemeConsolePkgString);
      public static readonly Guid guidIronSchemeConsoleCmdSet = new Guid(guidIronSchemeConsoleCmdSetString);
    };
}