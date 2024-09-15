global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.DataReferenceFinderModule;
using GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.QuickAttach;
using GameCodersToolkit.ReferenceFinder;
using GameCodersToolkit.ReferenceFinder.ToolWindows;
using GameCodersToolkit.Utils;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;

namespace GameCodersToolkit
{
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	[Guid(PackageGuids.GameCodersToolkitPackage_GuidString)]
	[ProvideToolWindow(typeof(ReferenceResultsWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.OutputWindow)]
	[ProvideToolWindow(typeof(DataExplorerWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
	[ProvideAutoLoad(VSConstants.UICONTEXT.SolutionOpening_string, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideService(typeof(ReferenceResultsWindowMessenger), IsAsyncQueryable = true)]
	[ProvideService(typeof(QuickAttachService), IsAsyncQueryable = true)]
	[ProvideOptionPage(typeof(OptionsProvider.QuickAttachOptionsOptions), "Game Coders Toolkit", "Quick Attach", 0, 0, true, SupportsProfiles = true)]
	public sealed class GameCodersToolkitPackage : ToolkitPackage
	{
		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			try
			{
				AddService(typeof(ReferenceResultsWindowMessenger), (_, _, _) => Task.FromResult<object>(new ReferenceResultsWindowMessenger()));
				AddService(typeof(QuickAttachService), (_, _, _) => Task.FromResult<object>(new QuickAttachService()));

				await this.RegisterCommandsAsync();
				this.RegisterToolWindows();
				FindReferenceResultsStorage = new FindReferenceResultsStorage();
				ExtensionOutput = await VS.Windows.CreateOutputWindowPaneAsync("GameCodersToolkit");

				DataLocationsConfig = new CDataLocationsConfiguration();
				await DataLocationsConfig.InitAsync();

				FileTemplateCreatorConfig = new CFileTemplateConfiguration();
				await FileTemplateCreatorConfig.InitAsync();

				DataParsingEngine = new DataParsingEngine();
				ReferenceDatabase = new Database();
				DataEditorConnection = new DataEditorConnection();
			}
			catch (Exception ex)
			{
				var output = await VS.Windows.GetOutputWindowPaneAsync(Community.VisualStudio.Toolkit.Windows.VSOutputWindowPane.General);
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"GameCodersToolkit Package Loading failed. Exception was thrown", 
					ex, 
					output);

				throw ex;
			}
		}

		public static OutputWindowPane ExtensionOutput { get; set; }

		internal static FindReferenceResultsStorage FindReferenceResultsStorage { get; private set; }
		public static CDataLocationsConfiguration DataLocationsConfig { get; private set; }
		public static CFileTemplateConfiguration FileTemplateCreatorConfig { get; private set; }
		public static DataParsingEngine DataParsingEngine {  get; private set; }
		public static DataReferenceFinderModule.ReferenceDatabase.Database ReferenceDatabase { get; private set; }
		public static DataEditorConnection DataEditorConnection { get; private set; }
	}
}