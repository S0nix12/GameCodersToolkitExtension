using GameCodersToolkit.ReferenceFinder.ToolWindows;
using GameCodersToolkit.ReferenceFinder;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase
{
	public static class ReferenceDatabaseUtils
	{
		public static async Task ExecuteFindOperationOnDatabaseAsync(GenericDataIdentifier identifier, string searchTerm)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			var resultsOutput = GameCodersToolkitPackage.FindReferenceResultsStorage.AddNewOperationEntry("Reference Database", searchTerm);
			resultsOutput.NotifyOperationStarted(GameCodersToolkitPackage.ReferenceDatabase.ReferencedByEntries.Count);
			if (GameCodersToolkitPackage.ReferenceDatabase.ReferencedByEntries.TryGetValue(identifier, out HashSet<DataEntry> entries))
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

					result.Text += " | " + entry.BaseType + " | " + entry.SubType;
					results.Enqueue(result);
				}
				resultsOutput.AddResults(results);
			}

			resultsOutput.NotifyOperationDone(stopwatch.Elapsed);
			stopwatch.Stop();

			await ReferenceResultsWindow.ShowAsync();
			await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync("Finding in database took " + stopwatch.ElapsedMilliseconds + "ms");
		}
	}
}
