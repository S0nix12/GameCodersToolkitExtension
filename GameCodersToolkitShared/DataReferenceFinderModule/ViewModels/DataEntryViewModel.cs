using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.Utils;
using Microsoft.VisualStudio.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.ViewModels
{
	public partial class DataEntryViewModel : ObservableObject, ISearchableViewModel
	{
		public DataEntryViewModel()
		{
			Init();
		}

		public DataEntryViewModel(DataEntry sourceEntry)
		{
			SourceEntry = sourceEntry;
			LineNumber = SourceEntry.SourceLineNumber;
			Name = SourceEntry.Name;
			DataPath = ReferenceDatabaseUtils.CreateDataEntryPathString(sourceEntry);
			Init();
		}

		private void Init()
		{
			DataEditorConnection dataEditorConnection = GameCodersToolkitPackage.DataEditorConnection;
			dataEditorConnection.DataEditorConnectionStatusChanged += OnDataEditorConnectionStatusChanged;
			openEntryInDataEditorCommand = new AsyncRelayCommand(OpenEntryInDataEditorAsync, CanExecuteOpenEntryInDataEditor);
			CanOpenInDataEditor = dataEditorConnection.IsConnectedToDataEditor;
		}

		[RelayCommand]
		private void FindReferences()
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate { await ReferenceDatabaseUtils.ExecuteFindOperationOnDatabaseAsync(SourceEntry.Identifier, Name); });
		}

		public async Task OpenEntryInDataEditorAsync()
		{
			await GameCodersToolkitPackage.DataEditorConnection.OpenInDataEditorAsync(SourceEntry);
		}
		public virtual async Task<bool> OpenInVisualStudioAsync()
		{
			if (SourceEntry != null)
			{
				DocumentView document = await VS.Documents.OpenInPreviewTabAsync(SourceEntry.SourceFile);
				if (document != null)
				{
					string searchTerm = SourceEntry.Identifier.ToString();
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
					return true;
				}
			}

			return false;
		}

		public bool CanExecuteOpenEntryInDataEditor()
		{
			return CanOpenInDataEditor;
		}

		void OnDataEditorConnectionStatusChanged(object sender, ConnectionStatusChangedEventArgs args)
		{
			CanOpenInDataEditor = args.IsConnected;
		}

		public string GetSearchField()
		{
			return Name;
		}

		private AsyncRelayCommand openEntryInDataEditorCommand;
		public IAsyncRelayCommand OpenEntryInDataEditorCommand { get => openEntryInDataEditorCommand; }

		private bool m_canOpenInDataEditor = false;
		public bool CanOpenInDataEditor { get => m_canOpenInDataEditor; set { SetProperty(ref m_canOpenInDataEditor, value); OpenEntryInDataEditorCommand.NotifyCanExecuteChanged(); } }

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }

		private DataEntry m_sourceEntry;
		public DataEntry SourceEntry { get => m_sourceEntry; set { m_sourceEntry = value; DataPath = ReferenceDatabaseUtils.CreateDataEntryPathString(m_sourceEntry); } }

		private string m_name;
		public string Name { get => m_name; set => m_name = value; }

		private string m_dataPath;
		public string DataPath { get => m_dataPath; set => SetProperty(ref m_dataPath, value); }

		private int m_lineNumber;

		public int LineNumber { get => m_lineNumber; set => m_lineNumber = value; }
	}
}
