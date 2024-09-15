using CommunityToolkit.Mvvm.ComponentModel;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.ReferenceFinder;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.ViewModels
{
	public class DataEntryResultViewModel : DataEntryViewModel
	{
		public DataEntryResultViewModel(FileResultsViewModel parentFileResult)
			: base()
		{
			m_parentFileResult = parentFileResult;
		}

		public override async Task<bool> OpenInVisualStudioAsync()
		{
			if (await base.OpenInVisualStudioAsync())
				return true;

			DocumentView document = await VS.Documents.OpenInPreviewTabAsync(m_parentFileResult.FilePath);
			if (document != null)
			{
				string searchTerm = m_parentFileResult.ParentOperationResult.SearchTerm;
				var lineSnapShot = document.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(LineNumber - 1);
				int searchTermIndex = lineSnapShot.GetText().IndexOf(searchTerm);
				if (searchTermIndex >= 0)
				{
					SnapshotSpan selectionSpan = new SnapshotSpan(lineSnapShot.Start.Add(searchTermIndex), searchTerm.Length);
					document.TextView.Selection.Select(selectionSpan, false);
					document.TextView.ViewScroller.EnsureSpanVisible(selectionSpan);
				}
				else
				{
					document.TextView.Caret.MoveTo(lineSnapShot.Start);
					document.TextView.Caret.EnsureVisible();
				}
			}

			return false;
		}

		private FileResultsViewModel m_parentFileResult;
	}

	public class FileResultsViewModel : ObservableObject
	{
		public FileResultsViewModel(OperationResultsViewModel inParentOperationResult)
		{
			ParentOperationResult = inParentOperationResult;
		}

		private string m_filePath = "";
		public string FilePath { get => m_filePath; set => SetProperty(ref m_filePath, value); }

		private ObservableCollection<DataEntryResultViewModel> m_dataEntryResults = new ObservableCollection<DataEntryResultViewModel>();
		public ObservableCollection<DataEntryResultViewModel> DataEntryResults { get => m_dataEntryResults; set => SetProperty(ref m_dataEntryResults, value); }

		public OperationResultsViewModel ParentOperationResult { get; private set; }

	}

	public class OperationResultsViewModel : ObservableObject
	{
		public OperationResultsViewModel(FindReferenceOperationResults model)
		{
			OperationId = model.OperationId;
			SearchPath = model.SearchPath;
			SearchTerm = model.SearchTerm;
			Status = model.Status;

			model.OperationStatusChanged += HandleOperationStatusChanged;
			model.ResultsUpdated += HandleResultsUpdate;
		}

		void HandleOperationStatusChanged(object operation, FindReferenceOperationResults.OperationStatusEventArgs eventArgs)
		{
			FindReferenceOperationResults model = (FindReferenceOperationResults)operation;
			Status = eventArgs.NewStatus;
			switch (Status)
			{
				case EFindReferenceOperationStatus.Pending:
					break;
				case EFindReferenceOperationStatus.InProgress:
					FilesToSearchCount = model.FilesToSearchCount;
					break;
				case EFindReferenceOperationStatus.Canceled:
					OperationDuration = model.OperationDuration;
					SearchedFileCount = model.SearchedFileCount;
					break;
				case EFindReferenceOperationStatus.Finished:
					OperationDuration = model.OperationDuration;
					SearchedFileCount = FilesToSearchCount;
					break;
			}
		}

		void HandleResultsUpdate(object operation, EventArgs eventArgs)
		{
			FindReferenceOperationResults model = (FindReferenceOperationResults)operation;
			UpdateResultsFromModel(model);
		}

		void UpdateResultsFromModel(FindReferenceOperationResults model)
		{
			foreach (var fileEntry in model.ResultsPerFile)
			{
				FileResultsViewModel? fileResultsViewModel = null;
				foreach (var fileResultVM in FileResults)
				{
					if (string.Compare(fileResultVM.FilePath, fileEntry.Key, StringComparison.OrdinalIgnoreCase) == 0)
					{
						fileResultsViewModel = fileResultVM;
						break;
					}
				}

				if (fileResultsViewModel == null)
				{
					fileResultsViewModel = new FileResultsViewModel(this);
					fileResultsViewModel.FilePath = fileEntry.Key;
					FileResults.Insert(0, fileResultsViewModel);
				}

				foreach (var dataResultEntry in fileEntry.Value)
				{
					if (!fileResultsViewModel.DataEntryResults.Any((existingResult) => { return existingResult.LineNumber == dataResultEntry.Line; }))
					{
						DataEntryResultViewModel newResult = new DataEntryResultViewModel(fileResultsViewModel);
						newResult.LineNumber = dataResultEntry.Line;
						newResult.Name = dataResultEntry.Text;
						newResult.SourceEntry = dataResultEntry.DataEntry;
						fileResultsViewModel.DataEntryResults.Insert(0, newResult);

						ResultsCount++;
					}
				}
			}
		}

		private string m_searchPath = "";
		public string SearchPath { get => m_searchPath; set => SetProperty(ref m_searchPath, value); }

		private string m_searchTerm;
		public string SearchTerm { get => m_searchTerm; set => SetProperty(ref m_searchTerm, value); }

		private int m_resultsCount;
		public int ResultsCount { get => m_resultsCount; set => SetProperty(ref m_resultsCount, value); }

		private int m_searchedFileCount;
		public int SearchedFileCount { get => m_searchedFileCount; set => SetProperty(ref m_searchedFileCount, value); }

		private int m_filesToSearchCount;
		public int FilesToSearchCount { get => m_filesToSearchCount; set => SetProperty(ref m_filesToSearchCount, value); }

		private TimeSpan m_operationDuration;
		public TimeSpan OperationDuration { get => m_operationDuration; set => SetProperty(ref m_operationDuration, value); }

		private EFindReferenceOperationStatus m_status = EFindReferenceOperationStatus.Pending;
		public EFindReferenceOperationStatus Status { get => m_status; set => SetProperty(ref m_status, value); }

		public Guid OperationId { get; private set; }

		private ObservableCollection<FileResultsViewModel> m_fileResults = new ObservableCollection<FileResultsViewModel>();
		public ObservableCollection<FileResultsViewModel> FileResults { get => m_fileResults; set => SetProperty(ref m_fileResults, value); }
	}

	public class ReferenceResultsWindowViewModel : ObservableObject
	{
		public ReferenceResultsWindowViewModel()
		{
			GameCodersToolkitPackage.FindReferenceResultsStorage.Results.CollectionChanged += HandleResultsCollectionChanged;
		}

		void HandleResultsCollectionChanged(object storage, NotifyCollectionChangedEventArgs eventArgs)
		{
			switch (eventArgs.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (FindReferenceOperationResults operation in eventArgs.NewItems)
					{
						OperationResults.Insert(0, new OperationResultsViewModel(operation));
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (FindReferenceOperationResults operation in eventArgs.OldItems)
					{
						OperationResults.RemoveAll((entry) => { return entry.OperationId == operation.OperationId; });
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					OperationResults.Clear();
					break;
			}
		}

		private ObservableCollection<OperationResultsViewModel> m_operationResults = new ObservableCollection<OperationResultsViewModel>();
		public ObservableCollection<OperationResultsViewModel> OperationResults { get => m_operationResults; set => SetProperty(ref m_operationResults, value); }
	}
}
