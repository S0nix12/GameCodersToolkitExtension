﻿global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using DataReferenceFinder.Configuration;
using DataReferenceFinder.ReferenceFinder;
using DataReferenceFinder.ToolWindows;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;

namespace DataReferenceFinder
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuids.DataReferenceFinderPackage_GuidString)]
	[ProvideToolWindow(typeof(ReferenceResultsWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.OutputWindow)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideService(typeof(ReferenceResultsWindowMessenger), IsAsyncQueryable = true)]
	public sealed class DataReferenceFinderPackage : ToolkitPackage
	{
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			try
			{
				AddService(typeof(ReferenceResultsWindowMessenger), (_, _, _) => Task.FromResult<object>(new ReferenceResultsWindowMessenger()));

				await this.RegisterCommandsAsync();
				this.RegisterToolWindows();
				FindReferenceResultsStorage = new CFindReferenceResultsStorage();
				ExtensionOutput = await VS.Windows.CreateOutputWindowPaneAsync("FindGuidOutput");

				DataLocationsConfig = new CDataLocationsConfiguration();
				await DataLocationsConfig.InitAsync();
			}
			catch (Exception ex)
			{
				var output = await VS.Windows.GetOutputWindowPaneAsync(Community.VisualStudio.Toolkit.Windows.VSOutputWindowPane.General);
				await output.WriteLineAsync("Data Reference Finder Package Loading failed. Exception was thrown");
				await output.WriteLineAsync(ex.Message);
				await output.WriteLineAsync(ex.StackTrace);
				System.Diagnostics.Debug.WriteLine(ex.Message);
				System.Diagnostics.Debug.WriteLine(ex.StackTrace);

				throw ex;
			}
		}

		public static OutputWindowPane ExtensionOutput { get; set; }

		internal static CFindReferenceResultsStorage FindReferenceResultsStorage { get; private set; }
		public static CDataLocationsConfiguration DataLocationsConfig { get; private set; }
	}
}