global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using DataReferenceFinder.Configuration;
using DataReferenceFinder.ReferenceFinder;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DataReferenceFinder
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuids.DataReferenceFinderPackage_GuidString)]
	[ProvideToolWindow(typeof(ReferenceResultsWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.OutputWindow)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
	public sealed class DataReferenceFinderPackage : ToolkitPackage
	{
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			await this.RegisterCommandsAsync();
			this.RegisterToolWindows();
			FindReferenceResultsStorage = new CFindReferenceResultsStorage();
			ExtensionOutput = await VS.Windows.CreateOutputWindowPaneAsync("FindGuidOutput");

			DataLocationsConfig = new CDataLocationsConfiguration();
			await DataLocationsConfig.InitAsync();
		}

		public static OutputWindowPane ExtensionOutput { get; set; }

		internal static CFindReferenceResultsStorage FindReferenceResultsStorage { get; private set; }
		public static CDataLocationsConfiguration DataLocationsConfig { get; private set; }
	}
}