using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace GameCodersToolkit.ReferenceFinder
{
	public class FoundReference
	{
		public int Line { get; set; } = 0;
		public string Text { get; set; } = "";
		public DataEntry DataEntry { get; set; } = null;
	}

	public enum EFindReferenceOperationStatus
	{
		Pending,
		InProgress,
		Canceled,
		Finished,
	}

	public struct PendingResult
	{
		public int Line { get; set; }
		public string Text { get; set; }
		public string File { get; set; }
		public DataEntry DataEntry { get; set; }
	}

	public class FindReferenceOperationResults
	{
		public FindReferenceOperationResults(string searchPath, string searchTerm)
		{
			SearchPath = searchPath;
			SearchTerm = searchTerm;
			OperationId = Guid.NewGuid();
		}

		public void AddResults(ConcurrentQueue<PendingResult> resultsToAdd)
		{
			if (Monitor.TryEnter(mutex))
			{
				PendingResult resultToAdd;
				while (resultsToAdd.TryDequeue(out resultToAdd))
				{
					AddResult(in resultToAdd);
				}
				Monitor.Exit(mutex);
			}
			else
			{
				Debug.Assert(false, "Concurrent access to this method is not permitted");
			}
			ResultsUpdated?.Invoke(this, new EventArgs());
		}

		void AddResult(in PendingResult inResult)
		{
			List<FoundReference> fileReferences = ResultsPerFile.GetOrCreate(inResult.File);
			fileReferences.Add(new FoundReference() { Line = inResult.Line, Text = inResult.Text, DataEntry = inResult.DataEntry });
			ResultsCount++;
		}

		public void NotifyOperationStarted(int numFilesToSerach)
		{
			EFindReferenceOperationStatus oldStatus = Status;

			Status = EFindReferenceOperationStatus.InProgress;
			FilesToSearchCount = numFilesToSerach;

			OnOperationStatusChange(oldStatus, Status);
		}

		public void NotifyOperationCanceled(int searchedFilesCount, in TimeSpan searchedDuration)
		{
			EFindReferenceOperationStatus oldStatus = Status;

			Status = EFindReferenceOperationStatus.Canceled;
			OperationDuration = searchedDuration;
			SearchedFileCount = searchedFilesCount;

			OnOperationStatusChange(oldStatus, Status);
		}

		public void NotifyOperationDone(in TimeSpan duration)
		{
			EFindReferenceOperationStatus oldStatus = Status;

			Status = EFindReferenceOperationStatus.Finished;
			OperationDuration = duration;

			OnOperationStatusChange(oldStatus, Status);
		}

		private void OnOperationStatusChange(EFindReferenceOperationStatus oldStatus, EFindReferenceOperationStatus newStatus)
		{
			OperationStatusEventArgs statusEventArgs = new OperationStatusEventArgs();
			statusEventArgs.PreviousStatus = oldStatus;
			statusEventArgs.NewStatus = newStatus;
			OperationStatusChanged?.Invoke(this, statusEventArgs);
		}

		public event EventHandler ResultsUpdated;

		public class OperationStatusEventArgs : EventArgs
		{
			public EFindReferenceOperationStatus PreviousStatus { get; set; }
			public EFindReferenceOperationStatus NewStatus { get; set; }
		}
		public event EventHandler<OperationStatusEventArgs> OperationStatusChanged;

		public string SearchPath { get; private set; }

		public string SearchTerm { get; private set; }

		public int ResultsCount { get; private set; }

		public int SearchedFileCount { get; set; }

		public int FilesToSearchCount { get; private set; }

		public TimeSpan OperationDuration { get; private set; }

		public EFindReferenceOperationStatus Status { get; private set; } = EFindReferenceOperationStatus.Pending;

		public Dictionary<string, List<FoundReference>> ResultsPerFile { get; private set; } = new Dictionary<string, List<FoundReference>>();

		public Guid OperationId { get; private set; }

		private object mutex = new object();
	}

	internal class FindReferenceResultsStorage
	{
		public FindReferenceOperationResults AddNewOperationEntry(string searchPath, string searchTerm)
		{
			Results.Add(new FindReferenceOperationResults(searchPath, searchTerm));
			return Results.Last();
		}

		public ObservableCollection<FindReferenceOperationResults> Results { get; private set; } = new ObservableCollection<FindReferenceOperationResults>();
	}
}
