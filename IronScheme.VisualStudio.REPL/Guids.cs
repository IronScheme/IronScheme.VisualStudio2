// Guids.cs
// MUST match guids.h
using System;
using System.Diagnostics.CodeAnalysis;

namespace IronScheme.VisualStudio.REPL
{
    public static class ConsoleGuidList
    {
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
      public const string guidIronSchemeConsolePkgString = "160FCEF2-DED9-4DC2-8E42-985446204ACE";

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly")]
        public const string guidIronSchemeConsoleCmdSetString = "9A3D771E-AA64-4098-AD87-BADA43A16F24";

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly")]
        public static readonly Guid guidIronSchemeConsolePkg = new Guid(guidIronSchemeConsolePkgString);

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly")]
        public static readonly Guid guidIronSchemeConsoleCmdSet = new Guid(guidIronSchemeConsoleCmdSetString);
    };
}