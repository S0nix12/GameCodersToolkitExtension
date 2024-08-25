using DataReferenceFinder.Configuration;
using DataReferenceFinder.ReferenceFinder;
using Microsoft.VisualStudio.Shell.Interop;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

namespace DataReferenceFinder
{
	struct SFoundLineEntry
	{
		public string lineText;
		public int lineNumber;
	}

	internal class CFileReferenceScanner
	{
		public CFileReferenceScanner(List<CDataLocationEntry> inSearchLocations, string inSearchString)
		{
			searchLocations = inSearchLocations;
			searchString = inSearchString;

			string searchPaths = "";
			foreach (CDataLocationEntry entry in searchLocations)
			{
				searchPaths += entry.Path;
				searchPaths += "; ";
			}
			ResultsOutput = DataReferenceFinderPackage.FindReferenceResultsStorage.AddNewOperationEntry(searchPaths, searchString);
		}

		public CFileReferenceScanner(string inPath, string inSearchString)
		{
			searchPath = inPath;
			searchString = inSearchString;

			ResultsOutput = DataReferenceFinderPackage.FindReferenceResultsStorage.AddNewOperationEntry(searchPath, searchString);
		}

		StreamReader CreateStreamReader(string path)
		{
			FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
			var readStream = new FileStream(
				path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

			return new StreamReader(readStream);
		}

		void ScanFile(string file)
		{
			using StreamReader reader = CreateStreamReader(file);
			string line = reader.ReadLine();
			int lineCounter = 0;
			while (line is not null)
			{
				lineCounter++;
				if (line.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					SFoundLineEntry entry = new SFoundLineEntry();
					entry.lineText = line;
					entry.lineNumber = lineCounter;

					var fileBag = FoundOccurences.GetOrAdd(file, new ConcurrentBag<SFoundLineEntry>());
					fileBag.Add(entry);

					SPendingResult pendingResult = new SPendingResult
					{
						Line = lineCounter,
						Text = line,
						File = file
					};
					pendingResults.Enqueue(pendingResult);
				}
				line = reader.ReadLine();
			}
			Interlocked.Increment(ref progressCounter);
		}

		async Task TransferResultsWorkerAsync(CancellationToken cancellationToken)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			while (!cancellationToken.IsCancellationRequested)
			{
				if (pendingResults.Count != 0)
				{
					ResultsOutput.AddResults(pendingResults);
				}
				ResultsOutput.SearchedFileCount = progressCounter;
				await Task.Delay(100);
			}
		}

		List<string> GetFilesToScan()
		{
			if (searchLocations == null || searchLocations.Count == 0)
			{
				return Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories).ToList();
			}
			else
			{
				IEnumerable<string> outFiles = new List<string>();
				foreach (CDataLocationEntry dataLocation in searchLocations)
				{
					if (dataLocation.ExtensionFilters.Count == 0)
					{
						outFiles = outFiles.Union(Directory.EnumerateFiles(dataLocation.Path, "*", SearchOption.AllDirectories));
					}
					else
					{
						outFiles = outFiles.Union(Directory
							.EnumerateFiles(dataLocation.Path, "*", SearchOption.AllDirectories)
							.Where(file => dataLocation.ExtensionFilters.Contains(Path.GetExtension(file).ToLower())));
					}
				}
				return outFiles.ToList();
			}
		}

		public async Task ScanAsync()
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			var files = GetFilesToScan();
			filesToScan = files.Count;
			ResultsOutput.NotifyOperationStarted(filesToScan);
			stopwatch.Stop();

			GetFilesDuration = stopwatch.Elapsed;
			stopwatch.Restart();

			IList<Task> scanFileTaskList = new List<Task>();
			foreach (var file in files)
			{
				scanFileTaskList.Add(Task.Run(() => { ScanFile(file); }));
			}

			CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
			CancellationToken transferResultsCancelToken = cancellationTokenSource.Token;
			Task transferResults = TransferResultsWorkerAsync(transferResultsCancelToken);

			await Task.WhenAll(scanFileTaskList);
			cancellationTokenSource.Cancel();
			await transferResults;

			stopwatch.Stop();
			ResultsOutput.NotifyOperationDone(stopwatch.Elapsed);
			ScanDuration = stopwatch.Elapsed;

			if (pendingResults.Count != 0)
			{
				ResultsOutput.AddResults(pendingResults);
			}
		}

		public void GetProgress(out int progress, out int total)
		{
			progress = progressCounter;
			total = filesToScan;
		}

		string searchPath = "";
		string searchString = "";

		List<CDataLocationEntry> searchLocations = new List<CDataLocationEntry>();

		int progressCounter = 0;
		int filesToScan = 1;
		ConcurrentQueue<SPendingResult> pendingResults = new ConcurrentQueue<SPendingResult>();

		public TimeSpan ScanDuration { get; private set; }
		public TimeSpan GetFilesDuration { get; private set; }

		public CFindReferenceOperationResults ResultsOutput { get; private set; }

		public ConcurrentDictionary<string, ConcurrentBag<SFoundLineEntry>> FoundOccurences { get; set; } = new ConcurrentDictionary<string, ConcurrentBag<SFoundLineEntry>>();
	}
}
