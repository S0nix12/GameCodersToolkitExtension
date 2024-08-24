using DataReferenceFinder.ReferenceFinder;
using System.Collections.Generic;

namespace DataReferenceFinder.Commands
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences)]
	internal sealed class FindGuidReferences : BaseCommand<FindGuidReferences>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			var textWriter = await DataReferenceFinderPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();

			var documentView = await VS.Documents.GetActiveDocumentViewAsync();
			var caretPosition = documentView.TextView?.Caret.Position;

			//string searchPath = "E:\\KlaxEngineProject_MockData\\ProjectData_1";
			string searchPath = "E:\\KlaxEngineProject_MockData";
			string searchText = await TextUtilFunctions.FindGuidUnderCaretAsync();

			if (string.IsNullOrEmpty(searchText))
			{
				await DataReferenceFinderPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("No Guid selection found");
				return;
			}
			try
			{
				CFileReferenceScanner scanner = new CFileReferenceScanner(searchPath, searchText);
				List<Task> taskList = new List<Task>();
				Task scanTask = Task.Run(scanner.ScanAsync);
				Task progressUpdateTask = ShowScannerProgressAsync(scanner);

				await ReferenceResultsWindow.ShowAsync();
				await progressUpdateTask;
			}
			catch (Exception ex)
			{
				await DataReferenceFinderPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("Exeception occured:");
				await textWriter.WriteLineAsync(ex.Message);
				await textWriter.WriteLineAsync(ex.StackTrace);
				await textWriter.WriteLineAsync(ex.ToString());
			}
		}

		// Somehow this does not update correctly. Visual studio does not call this function reliabily when trying to execute a command or opening the menu in which it is located
		// This is super frustrating for the user as it might not invoke the command when it could get invoked. Better leave it active all the time
		//protected override void BeforeQueryStatus(EventArgs e)
		//{			
		//	string guidText = ThreadHelper.JoinableTaskFactory.Run(TextUtilFunctions.FindGuidUnderCaretAsync);
		//	Command.Enabled = !string.IsNullOrEmpty(guidText);
		//}

		async Task ShowScannerProgressAsync(CFileReferenceScanner scanner)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			int progress = 0;
			int total = 0;

			do
			{
				scanner.GetProgress(out progress, out total);
				string progressText = string.Format("Searching Files: {0}/{1}", progress, total);
				await VS.StatusBar.ShowProgressAsync(progressText, progress + 1, total + 1);
				if (total != 1)
					await Task.Delay(100);
			}
			while (progress < total);
		}
	}
}
