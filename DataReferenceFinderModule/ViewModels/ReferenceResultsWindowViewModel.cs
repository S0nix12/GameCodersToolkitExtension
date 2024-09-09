using CommunityToolkit.Mvvm.ComponentModel;
using GameCodersToolkit.ReferenceFinder;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace GameCodersToolkit.DataReferenceFinderModule.ViewModels
{
	public class CLineResultViewModel : ObservableObject
	{
		public CLineResultViewModel(CFileResultsViewModel parentFileResult)
		{
			m_parentFileResult = parentFileResult;
		}

		public async Task ShowEntryAsync()
		{
			DocumentView document = await VS.Documents.OpenInPreviewTabAsync(m_parentFileResult.FilePath);
			string searchTerm = m_parentFileResult.ParentOperationResult.SearchTerm;
			if (document != null)
			{
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
		}

		private string m_lineText = "";
		public string LineText { get => m_lineText; set => SetProperty(ref m_lineText, value); }

		private int m_lineNumber = 0;
		public int LineNumber { get => m_lineNumber; set => SetProperty(ref m_lineNumber, value); }

		private CFileResultsViewModel m_parentFileResult;
	}

	public class CFileResultsViewModel : ObservableObject
	{
		public CFileResultsViewModel(COperationResultsViewModel inParentOperationResult)
		{
			ParentOperationResult = inParentOperationResult;
		}

		private string m_filePath = "";
		public string FilePath { get => m_filePath; set => SetProperty(ref m_filePath, value); }

		private ObservableCollection<CLineResultViewModel> m_lineResults = new ObservableCollection<CLineResultViewModel>();
		public ObservableCollection<CLineResultViewModel> LineResults { get => m_lineResults; set => SetProperty(ref m_lineResults, value); }

		public COperationResultsViewModel ParentOperationResult { get; private set; }

	}

	public class COperationResultsViewModel : ObservableObject
	{
		public COperationResultsViewModel(CFindReferenceOperationResults model)
		{
			OperationId = model.OperationId;
			SearchPath = model.SearchPath;
			SearchTerm = model.SearchTerm;
			Status = model.Status;

			model.OperationStatusChanged += HandleOperationStatusChanged;
			model.ResultsUpdated += HandleResultsUpdate;
		}

		void HandleOperationStatusChanged(object operation, CFindReferenceOperationResults.OperationStatusEventArgs eventArgs)
		{
			CFindReferenceOperationResults model = (CFindReferenceOperationResults)operation;
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
			CFindReferenceOperationResults model = (CFindReferenceOperationResults)operation;
			UpdateResultsFromModel(model);
		}

		void UpdateResultsFromModel(CFindReferenceOperationResults model)
		{
			foreach (var fileEntry in model.ResultsPerFile)
			{
				CFileResultsViewModel? fileResultsViewModel = null;
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
					fileResultsViewModel = new CFileResultsViewModel(this);
					fileResultsViewModel.FilePath = fileEntry.Key;
					FileResults.Insert(0, fileResultsViewModel);
				}

				foreach (var lineResultEntry in fileEntry.Value)
				{
					if (!fileResultsViewModel.LineResults.Any((existingResult) => { return existingResult.LineNumber == lineResultEntry.Line; }))
					{
						CLineResultViewModel newResult = new CLineResultViewModel(fileResultsViewModel);
						newResult.LineNumber = lineResultEntry.Line;
						newResult.LineText = lineResultEntry.Text;
						fileResultsViewModel.LineResults.Insert(0, newResult);

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

		private ObservableCollection<CFileResultsViewModel> m_fileResults = new ObservableCollection<CFileResultsViewModel>();
		public ObservableCollection<CFileResultsViewModel> FileResults { get => m_fileResults; set => SetProperty(ref m_fileResults, value); }
	}

	public class CReferenceResultsWindowViewModel : ObservableObject
	{
		public CReferenceResultsWindowViewModel()
		{
			GameCodersToolkitPackage.FindReferenceResultsStorage.Results.CollectionChanged += HandleResultsCollectionChanged;
		}

		void HandleResultsCollectionChanged(object storage, NotifyCollectionChangedEventArgs eventArgs)
		{
			switch (eventArgs.Action)
			{
				case NotifyCollectionChangedAction.Add:
					foreach (CFindReferenceOperationResults operation in eventArgs.NewItems)
					{
						OperationResults.Insert(0, new COperationResultsViewModel(operation));
					}
					break;
				case NotifyCollectionChangedAction.Remove:
					foreach (CFindReferenceOperationResults operation in eventArgs.OldItems)
					{
						OperationResults.RemoveAll((entry) => { return entry.OperationId == operation.OperationId; });
					}
					break;
				case NotifyCollectionChangedAction.Reset:
					OperationResults.Clear();
					break;
			}
		}

		private ObservableCollection<COperationResultsViewModel> m_operationResults = new ObservableCollection<COperationResultsViewModel>();
		public ObservableCollection<COperationResultsViewModel> OperationResults { get => m_operationResults; set => SetProperty(ref m_operationResults, value); }
	}
}
