global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using DataReferenceFinder.ReferenceFinder;
using System.Runtime.InteropServices;
using System.Threading;

namespace DataReferenceFinder
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuids.DataReferenceFinderPackage_GuidString)]
	[ProvideToolWindow(typeof(ReferenceResultsWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.OutputWindow)]
	public sealed class DataReferenceFinderPackage : ToolkitPackage
	{
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await this.RegisterCommandsAsync();
			this.RegisterToolWindows();
			FindReferenceResultsStorage = new CFindReferenceResultsStorage();
			ExtensionOutput = await VS.Windows.CreateOutputWindowPaneAsync("FindGuidOutput");
		}

		public static OutputWindowPane ExtensionOutput { get; set; }

		internal static CFindReferenceResultsStorage FindReferenceResultsStorage { get; private set; }
	}


}