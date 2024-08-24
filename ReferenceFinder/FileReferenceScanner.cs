using DataReferenceFinder.ReferenceFinder;
using Microsoft.VisualStudio.Threading;
using stdole;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.VisualStudio.Threading.AsyncReaderWriterLock;

namespace DataReferenceFinder
{
	struct SFoundLineEntry
	{
		public string lineText;
		public int lineNumber;
	}

	internal class CFileReferenceScanner
	{
		public CFileReferenceScanner(string inPath, string inSearchString)
		{
			searchPath = inPath;
			searchString = inSearchString;

			ResultsOutput = DataReferenceFinderPackage.FindReferenceResultsStorage.AddNewOperationEntry(searchPath, searchString);
			FoundOccurences = new ConcurrentDictionary<string, ConcurrentBag<SFoundLineEntry>>();
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

		public async Task ScanAsync()
		{
			var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);
			filesToScan = files.Length;
			ResultsOutput.NotifyOperationStarted(filesToScan);

			Stopwatch stopwatch = Stopwatch.StartNew();
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

			if (pendingResults.Count != 0)
			{
				ResultsOutput.AddResults(pendingResults);
			}
			stopwatch.Stop();
			ResultsOutput.NotifyOperationDone(stopwatch.Elapsed);
			scanDurationMilliseconds = stopwatch.ElapsedMilliseconds;
		}

		public void GetProgress(out int progress, out int total)
		{
			progress = progressCounter;
			total = filesToScan;
		}

		public long GetLastScanDurationMs()
		{
			return scanDurationMilliseconds;
		}

		string searchPath = "";
		string searchString = "";

		long scanDurationMilliseconds = 0;

		int progressCounter = 0;
		int filesToScan = 1;
		ConcurrentQueue<SPendingResult> pendingResults = new ConcurrentQueue<SPendingResult>();

		internal CFindReferenceOperationResults ResultsOutput { get; private set; }

		public ConcurrentDictionary<string, ConcurrentBag<SFoundLineEntry>> FoundOccurences { get; set; }
	}
}
