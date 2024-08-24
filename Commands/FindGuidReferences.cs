using DataReferenceFinder.ReferenceFinder;
using System.Collections.Generic;
using System.Linq;

namespace DataReferenceFinder.Commands
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferences)]
	internal sealed class FindGuidReferences : BaseCommand<FindGuidReferences>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			var outputWindow = await VS.Services.GetOutputWindowAsync();
			
			await DataReferenceFinderPackage.ExtensionOutput.ActivateAsync();
			var textWriter = await DataReferenceFinderPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();

			var documentView = await VS.Documents.GetActiveDocumentViewAsync();
			var caretPosition = documentView.TextView?.Caret.Position;


			//string searchPath = "E:\\KlaxEngineProject_MockData\\ProjectData_1";
			string searchPath = "E:\\KlaxEngineProject_MockData";
			Guid searchGuid = new Guid("6696a24d-7a9c-489d-b9ef-7b6e775a24df");
			string searchText = await TextUtilFunctions.FindGuidUnderCaretAsync();

			if (string.IsNullOrEmpty(searchText))
			{
				await textWriter.WriteLineAsync("No Guid selection found");
				return;
			}
			try
			{
				CFileReferenceScanner scanner = new CFileReferenceScanner(searchPath, searchText);
				List<Task> taskList = new List<Task>();
				Task scanTask = Task.Run(scanner.ScanAsync);
				Task progressUpdateTask = ShowScannerProgressAsync(scanner);

				await textWriter.WriteLineAsync(string.Format("Searching references for: {0} in files: {1}", searchGuid.ToString(), searchPath));
				await scanTask;
				await textWriter.WriteLineAsync(string.Format("Searching for references done. Scan took: {0}ms", scanner.GetLastScanDurationMs()));

				foreach (var fileEntry in scanner.FoundOccurences)
				{
					await textWriter.WriteLineAsync(string.Format("	Found {0} References in file: {1}", fileEntry.Value.Count, fileEntry.Key));
					List<SFoundLineEntry> foundEntries = fileEntry.Value.ToList();
					foundEntries.Sort((a, b) => { return a.lineNumber - b.lineNumber; });

					foreach (var entry in foundEntries)
					{
						await textWriter.WriteLineAsync(string.Format("		Line {0}: {1}", entry.lineNumber, entry.lineText));
					}
				}
				await progressUpdateTask;
			}
			catch (Exception ex)
			{
				await textWriter.WriteLineAsync("Exeception occured:");
				await textWriter.WriteLineAsync(ex.Message);
				await textWriter.WriteLineAsync(ex.StackTrace);
				await textWriter.WriteLineAsync(ex.ToString());
			}
		}

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
