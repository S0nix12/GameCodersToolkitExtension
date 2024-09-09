using GameCodersToolkit.Configuration;
using Microsoft.VisualStudio.TaskStatusCenter;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

		public async Task StartDataParseAsync()
		{
			IVsTaskStatusCenterService taskStatusCenter = await VS.Services.GetTaskStatusCenterAsync();
			var taskOptions = default(TaskHandlerOptions);
			taskOptions.Title = "Parsing Data Files";
			taskOptions.ActionsAfterCompletion = CompletionActions.None;

			TaskProgressData taskData = default;
			taskData.CanBeCanceled = false;

			ITaskHandler taskHandler = taskStatusCenter.PreRegister(taskOptions, taskData);
			Task parseTask = Task.Run(async delegate { await ParseAllDataLocationsAsync(taskHandler, taskData); });
			taskHandler.RegisterTask(parseTask);
		}

		public async Task ParseAllDataLocationsAsync(ITaskHandler taskHandler, TaskProgressData progressData)
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

			Task parsingTask = Task.Run(async delegate { await ParseWaitingOperationsAsync(taskHandler, waitingParseOperations, totalCountToParse); });
			foreach (var locationEntry in filesToParse)
			{
				foreach (string filePath in locationEntry.Item1)
				{
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
		}

		async Task ParseWaitingOperationsAsync(ITaskHandler taskHandler, ConcurrentQueue<DataFileParser> operationQueue, int totalCountToParse)
		{
			int progressCounter = 0;
			while (progressCounter < totalCountToParse)
			{
				using (SemaphoreSlim semaphore = new SemaphoreSlim(MaxConcurrentParseOperation))
				{
					List<Task> runningOperations = new List<Task>();
					while (operationQueue.TryDequeue(out DataFileParser nextOperation))
					{
						await semaphore.WaitAsync();
						Task nextTask = Task.Run(async delegate 
						{ 
							try
							{
								await ExecuteParseOperationAsync(nextOperation);
							}
							finally
							{
								semaphore.Release();
								Interlocked.Increment(ref progressCounter);
								
								TaskProgressData progressData = new TaskProgressData();
								progressData.ProgressText = $"Parsing files: {progressCounter} of {totalCountToParse} done";
								progressData.PercentComplete = (int)(((float)progressCounter) / totalCountToParse * 100);
								taskHandler.Progress.Report(progressData);
							}
						});

						runningOperations.Add(nextTask);
					}

					await Task.WhenAll(runningOperations);
				}
			}
		}

		async Task ExecuteParseOperationAsync(DataFileParser operation)
		{
			DataParsingErrorList errorList = new DataParsingErrorList();
			List<DataEntry> parsedEntries = operation.Parse(errorList);
			parsedEntries.TrimExcess();

			var database = GameCodersToolkitPackage.ReferenceDatabase;
			database.ClearEntriesForFile(operation.FilePath);
			database.AddEntriesForFile(operation.FilePath, new HashSet<DataEntry>(parsedEntries));

			if (errorList.HasEntries())
			{
				var textWriter = await GameCodersToolkitPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();
				errorList.DumpToOutput(textWriter);
			}
		}

		class WaitingParseOperation
		{
			public string FilePath { get; set; }
			public string FileContent { get; set; }
			public List<DataParsingDescription> ParsingDescriptions { get; set; }
		}
	}
}
