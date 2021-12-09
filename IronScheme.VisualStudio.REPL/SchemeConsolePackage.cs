using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace IronScheme.VisualStudio.REPL
{
    /// <summary>
    /// This class implements the package responsible for the integration of the IronScheme
    /// console window in Visual Studio.
    /// There are two main aspects in this integration: the first one is to expose a service
    /// that will allow other packages to get a reference to the IronScheme engine used by
    /// the console or create a new one; the second part of the integration is the creation
    /// of the console as a Visual Studio tool window.
    /// </summary>
    // This attribute tells the registration utility (regpkg.exe) that this class needs
    // to be registered as package.
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    // This attribute is used to register a Visual Studio service so that the shell will load
    // this package if another package is querying for this service.
    //[ProvideService(typeof(ISchemeEngineProvider))]
    // Set the information needed for the shell to know about this tool window and to know
    // how to persist its data.
    [ProvideToolWindow(typeof(ConsoleWindow))]
    // With this attribute we notify the shell that we are defining some menu in the VSCT file.
    [ProvideMenuResource(1000, 1)]
    // The GUID of the package.
    [Guid(ConsoleGuidList.guidIronSchemeConsolePkgString)]
    public sealed class SchemeConsolePackage : AsyncPackage
    {
        /// <summary>
        /// Initialization function for the package.
        /// When this function is called, the package is sited, so it is possible to use the standard
        /// Visual Studio services.
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();
            // Always call the base implementation of Initialize.

            // Add our command handlers for menu (commands must exist in the .vsct file)
            OleMenuCommandService mcs = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (null == mcs)
            {
                // If it is not possible to get the command service, then there is nothing to do.
                return;
            }

            // Create the command for the tool window
            CommandID toolwndCommandID = new CommandID(ConsoleGuidList.guidIronSchemeConsoleCmdSet, (int)PkgCmdIDList.cmdidIronSchemeConsole);
            MenuCommand menuToolWin = new MenuCommand((s, e) => Execute(), toolwndCommandID);
            mcs.AddCommand(menuToolWin);
        }

        protected override async Task<object> InitializeToolWindowAsync(Type toolWindowType, int id, CancellationToken cancellationToken)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            ToolWindowPane pane = await this.FindToolWindowAsync(typeof(ConsoleWindow), 0, true, cancellationToken);
            if (null == pane)
            {
                throw new COMException(Resources.CanNotCreateConsole);
            }

            IVsWindowFrame frame = pane.Frame as IVsWindowFrame;
            if (null == frame)
            {
                throw new COMException(Resources.CanNotCreateConsole);
            }

            frame.Show();

            return frame;
        }

        private void Execute()
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                var window = await ShowToolWindowAsync(
              typeof(ConsoleWindow),
              0,
              create: true,
              cancellationToken: DisposalToken);
            });
        }

    }
}