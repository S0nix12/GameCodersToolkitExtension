using GameCodersToolkit.Configuration;
using Microsoft.VisualStudio.TaskStatusCenter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase
{
	public class DataParsingEngine
	{
		const int MaxConcurrentParseOperation = 8;

		public DataParsingEngine()
		{
			GameCodersToolkitPackage.DataLocationsConfig.SolutionConfigLoaded += OnSolutionConfigLoaded;
		}

		public async Task ParseDataAsync()
		{
			m_parseTaskCancallationSource?.Cancel();
			Stopwatch stopwatch = Stopwatch.StartNew();

			IVsTaskStatusCenterService taskStatusCenter = await VS.Services.GetTaskStatusCenterAsync();
			var taskOptions = default(TaskHandlerOptions);
			taskOptions.Title = "Parsing Data Files";
			taskOptions.ActionsAfterCompletion = CompletionActions.None;

			TaskProgressData taskData = default;
			taskData.CanBeCanceled = true;
			
			try
			{
				await m_parseTaskSemaphore.WaitAsync();
				GameCodersToolkitPackage.ReferenceDatabase.ClearDatabase();
				m_parseTaskCancallationSource = new CancellationTokenSource();
				ITaskHandler taskHandler = taskStatusCenter.PreRegister(taskOptions, taskData);
				Task parseTask = Task.Run(async delegate { await ParseAllDataLocationsAsync(taskHandler, taskData); });
				taskHandler.RegisterTask(parseTask);
				await parseTask;
				stopwatch.Stop();
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync("Finished Data Parsing. Took: " + stopwatch.ElapsedMilliseconds + "ms");
			}
			finally
			{
				m_parseTaskSemaphore.Release();
			}
		}

		private async Task ParseAllDataLocationsAsync(ITaskHandler taskHandler, TaskProgressData progressData)
		{
			progressData.ProgressText = $"Gathering files to parse";
			progressData.PercentComplete = 0;
			taskHandler.Progress.Report(progressData);

			ConcurrentQueue<DataFileParser> waitingParseOperations = new ConcurrentQueue<DataFileParser>();
			List<Tuple<string[], List<DataParsingDescription>>> filesToParse = new List<Tuple<string[], List<DataParsingDescription>>>();

			List<CDataLocationEntry> dataLocationEntries = GameCodersToolkitPackage.DataLocationsConfig.GetLocationEntries();
			foreach (CDataLocationEntry dataLocation in dataLocationEntries)
			{
				List<DataParsingDescription> parsingDescriptions =
					GameCodersToolkitPackage.DataLocationsConfig.GetParsingDescriptions()
					.Where(desc => dataLocation.UsedParsingDescriptions.Contains(desc.Name))
					.ToList();

				if (parsingDescriptions.Count == 0)
				{
					continue;
				}

				string[] fileList;
				if (dataLocation.ExtensionFilters.Count == 0)
				{
					fileList = Directory.GetFiles(dataLocation.Path, "*.xml", SearchOption.AllDirectories);
				}
				else
				{
					fileList = Directory.EnumerateFiles(dataLocation.Path, "*.xml", SearchOption.AllDirectories)
						.Where(path => dataLocation.ExtensionFilters.Contains(Path.GetExtension(path))).ToArray();
				}
				filesToParse.Add(new Tuple<string[], List<DataParsingDescription>>(fileList, parsingDescriptions));
			}

			int totalCountToParse = filesToParse.Sum(entry => entry.Item1.Count());

			progressData.ProgressText = $"Parsing files: 0 of {totalCountToParse} done";
			progressData.PercentComplete = 0;
			taskHandler.Progress.Report(progressData);

			CancellationToken cancellationToken = m_parseTaskCancallationSource.Token;

			Task parsingTask = Task.Run(async delegate { await ParseWaitingOperationsAsync(taskHandler, waitingParseOperations, totalCountToParse); });
			foreach (var locationEntry in filesToParse)
			{
				if (cancellationToken.IsCancellationRequested || taskHandler.UserCancellation.IsCancellationRequested)
					break;

				foreach (string filePath in locationEntry.Item1)
				{
					if (cancellationToken.IsCancellationRequested || taskHandler.UserCancellation.IsCancellationRequested)
						break;

					string fileContent = "";
					try
					{
						fileContent = File.ReadAllText(filePath);
					}
					finally
					{
						DataFileParser parser = new DataFileParser(fileContent, filePath, locationEntry.Item2);
						waitingParseOperations.Enqueue(parser);
					}
				}
			}

			await parsingTask;
			GameCodersToolkitPackage.ReferenceDatabase.TrimDatabaseExcess();
			GC.Collect();

			if (cancellationToken.IsCancellationRequested || taskHandler.UserCancellation.IsCancellationRequested)
			{
				progressData.ProgressText = $"Parsing cancelled";
				progressData.PercentComplete = 0;
				taskHandler.Progress.Report(progressData);
			}
		}

		async Task ParseWaitingOperationsAsync(ITaskHandler taskHandler, ConcurrentQueue<DataFileParser> operationQueue, int totalCountToParse)
		{
			int progressCounter = 0;
			CancellationToken cancellationToken = m_parseTaskCancallationSource.Token;
			while (progressCounter < totalCountToParse)
			{
				if (cancellationToken.IsCancellationRequested || taskHandler.UserCancellation.IsCancellationRequested)
					break;

				using (SemaphoreSlim semaphore = new SemaphoreSlim(MaxConcurrentParseOperation))
				{
					List<Task> runningOperations = new List<Task>();
					while (operationQueue.TryDequeue(out DataFileParser nextOperation))
					{
						if (cancellationToken.IsCancellationRequested || taskHandler.UserCancellation.IsCancellationRequested)
							break;

						await semaphore.WaitAsync();
						Task nextTask = Task.Run(async delegate
						{
							try
							{
								await ExecuteParseOperationAsync(nextOperation, cancellationToken);
							}
							finally
							{
								semaphore.Release();
								Interlocked.Increment(ref progressCounter);

								TaskProgressData progressData = new TaskProgressData();
								progressData.CanBeCanceled = true;
								progressData.ProgressText = $"Parsing files: {progressCounter} of {totalCountToParse} done";
								progressData.PercentComplete = (int)(((float)progressCounter) / totalCountToParse * 100);
								taskHandler.Progress.Report(progressData);
							}
						}, cancellationToken);

						runningOperations.Add(nextTask);
					}

					await Task.WhenAll(runningOperations);
				}
			}
		}

		async Task ExecuteParseOperationAsync(DataFileParser operation, CancellationToken cancellationToken)
		{
			DataParsingErrorList errorList = new DataParsingErrorList();
			List<DataEntry> parsedEntries = operation.Parse(errorList, cancellationToken);
			parsedEntries.TrimExcess();

			var database = GameCodersToolkitPackage.ReferenceDatabase;
			database.ClearEntriesForFile(operation.FilePath);
			if (parsedEntries.Count > 0)
			{
				database.AddEntriesForFile(operation.FilePath, new HashSet<DataEntry>(parsedEntries));
			}

			if (errorList.HasEntries())
			{
				var textWriter = await GameCodersToolkitPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();
				errorList.DumpToOutput(textWriter);
			}
		}

		private void OnSolutionConfigLoaded(object sender, EventArgs args)
		{
			DataReferenceFinderOptions userOptions = DataReferenceFinderOptions.Instance;
			if (userOptions.ReferenceDatabaseAutoParseOnSolutionLoad)
			{
				Task.Run(ParseDataAsync).FireAndForget();
			}
		}

		private SemaphoreSlim m_parseTaskSemaphore = new SemaphoreSlim(1);
		private CancellationTokenSource m_parseTaskCancallationSource;

		class WaitingParseOperation
		{
			public string FilePath { get; set; }
			public string FileContent { get; set; }
			public List<DataParsingDescription> ParsingDescriptions { get; set; }
		}
	}
}