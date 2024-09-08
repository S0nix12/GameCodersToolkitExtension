using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.ReferenceFinder;
using GameCodersToolkit.ReferenceFinder.ToolWindows;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferencesInDatabase)]
	internal sealed class FindGuidReferencesInDatabaseCommand : BaseCommand<FindGuidReferencesInDatabaseCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			var textWriter = await GameCodersToolkitPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();

			string searchText = await TextUtilFunctions.FindGuidUnderCaretAsync();

			if (string.IsNullOrEmpty(searchText))
			{
				await GameCodersToolkitPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("No Guid selection found");
				return;
			}

			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				var resultsOutput = GameCodersToolkitPackage.FindReferenceResultsStorage.AddNewOperationEntry("Reference Database", searchText);
				Guid guid = Guid.Parse(searchText);
				resultsOutput.NotifyOperationStarted(GameCodersToolkitPackage.ReferenceDatabase.ReferencedByEntries.Count);
				if (GameCodersToolkitPackage.ReferenceDatabase.ReferencedByEntries.TryGetValue(new GenericDataIdentifier(guid), out HashSet<DataEntry> entries))
				{
					ConcurrentQueue<SPendingResult> results = new ConcurrentQueue<SPendingResult>();
					foreach (var entry in entries)
					{
						SPendingResult result = new SPendingResult();
						result.File = entry.SourceFile;
						result.Line = entry.SourceLineNumber;

						string resultPath = entry.Name;
						DataEntry parentEntry = entry.Parent;
						while (parentEntry != null)
						{
							resultPath = parentEntry.Name + " -> " + resultPath;
							if (parentEntry.Parent == null && !string.IsNullOrEmpty(parentEntry.ParentName))
							{
								resultPath = parentEntry.ParentName + " -> " + resultPath;
							}
							parentEntry = parentEntry.Parent;
						}
						result.Text = resultPath;
						results.Enqueue(result);
					}
					resultsOutput.AddResults(results);
				}

				resultsOutput.NotifyOperationDone(stopwatch.Elapsed);
				stopwatch.Stop();

				await ReferenceResultsWindow.ShowAsync();
				await textWriter.WriteLineAsync("Finding in database took " + stopwatch.ElapsedMilliseconds + "ms");
			}
			catch (Exception ex)
			{
				await GameCodersToolkitPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("Exeception occured:");
				await textWriter.WriteLineAsync(ex.Message);
				await textWriter.WriteLineAsync(ex.StackTrace);
				await textWriter.WriteLineAsync(ex.ToString());
			}
		}
		protected override void BeforeQueryStatus(EventArgs e)
		{
			string guidText = ThreadHelper.JoinableTaskFactory.Run(TextUtilFunctions.FindGuidUnderCaretAsync);
			Command.Enabled = !string.IsNullOrEmpty(guidText);
		}
	}
}
