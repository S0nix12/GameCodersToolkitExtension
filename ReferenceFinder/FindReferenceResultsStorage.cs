using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace DataReferenceFinder.ReferenceFinder
{
	public class CFoundReference
	{
		public int Line { get; set; } = 0;
		public string Text { get; set; } = "";
	}

	public enum EFindReferenceOperationStatus
	{
		Pending,
		InProgress,
		Canceled,
		Finished,
	}

	public struct SPendingResult
	{
		public int Line { get; set; }
		public string Text { get; set; }
		public string File { get; set; }
	}

	public class CFindReferenceOperationResults
	{
		public CFindReferenceOperationResults(string searchPath, string searchTerm)
		{
			SearchPath = searchPath;
			SearchTerm = searchTerm;
			OperationId = Guid.NewGuid();
		}

		public void AddResults(ConcurrentQueue<SPendingResult> resultsToAdd)
		{
			if (Monitor.TryEnter(mutex))
			{
				SPendingResult resultToAdd;
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

		void AddResult(in SPendingResult inResult)
		{
			List<CFoundReference> fileReferences = ResultsPerFile.GetOrCreate(inResult.File);
			fileReferences.Add(new CFoundReference() { Line = inResult.Line, Text = inResult.Text });
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

		public Dictionary<string, List<CFoundReference>> ResultsPerFile { get; private set; } = new Dictionary<string, List<CFoundReference>>();

		public Guid OperationId { get; private set; }

		private object mutex = new object();
	}

	internal class CFindReferenceResultsStorage
	{
		public CFindReferenceOperationResults AddNewOperationEntry(string searchPath, string searchTerm)
		{
			Results.Add(new CFindReferenceOperationResults(searchPath, searchTerm));
			return Results.Last();
		}

		public ObservableCollection<CFindReferenceOperationResults> Results { get; private set; } = new ObservableCollection<CFindReferenceOperationResults>();
	}
}
