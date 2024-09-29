using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataReferenceCodeLensProviderShared.Communication;
using GameCodersToolkit;
using GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Xml.Linq;

namespace GameCodersToolkitShared.DataReferenceFinderModule.CodeLens
{
	public class CodeLensDataReferenceDetailsViewModel : ObservableObject
	{
		public CodeLensDataReferenceDetailsViewModel(CodeLensDataReferenceDetails details)
		{
			SourceFile = details.SourceFile;
			DataPath = details.ParentPath;
			LineNumber = details.SourceLineNumber;
			SubType = details.SubType;
			m_sourceDetails = details;

			DataEditorConnection dataEditorConnection = GameCodersToolkitPackage.DataEditorConnection;
			dataEditorConnection.DataEditorConnectionStatusChanged += OnDataEditorConnectionStatusChanged;
			openEntryInDataEditorCommand = new AsyncRelayCommand(OpenEntryInDataEditorAsync, CanExecuteOpenEntryInDataEditor);
			CanOpenInDataEditor = dataEditorConnection.IsConnectedToDataEditor;
		}
		public async Task OpenEntryInDataEditorAsync()
		{
			OpenDataEntryMessage message = new OpenDataEntryMessage
			{
				IdentifierString = m_sourceDetails.IdentifierString,
				Name = m_sourceDetails.Name,
				SourceFile = SourceFile,
				TypeName = m_sourceDetails.TypeName,
				SubTypeName = SubType,
				SourceLineNumber = LineNumber,
				ParentIdentifierString = m_sourceDetails.ParentIdentifierString
			};
			await GameCodersToolkitPackage.DataEditorConnection.OpenInDataEditorAsync(message);
		}

		public virtual async Task<bool> OpenInVisualStudioAsync()
		{
			DocumentView document = await VS.Documents.OpenInPreviewTabAsync(SourceFile);
			if (document != null)
			{
				string searchTerm = m_sourceDetails.IdentifierString;
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

		private AsyncRelayCommand openEntryInDataEditorCommand;
		public IAsyncRelayCommand OpenEntryInDataEditorCommand { get => openEntryInDataEditorCommand; }

		private bool m_canOpenInDataEditor = false;
		public bool CanOpenInDataEditor { get => m_canOpenInDataEditor; set { SetProperty(ref m_canOpenInDataEditor, value); OpenEntryInDataEditorCommand.NotifyCanExecuteChanged(); } }

		public string SourceFile { get; set; }
		public string SubType { get; set; }
		public string DataPath { get; set; }
		public int LineNumber { get; set; }
		private CodeLensDataReferenceDetails m_sourceDetails;
	}

	public class CodeLensDataReferenceCustomViewModel
	{
		public CodeLensDataReferenceCustomViewModel(CodeLensDataReferenceCustomData customData)
		{
			m_detailEntries = new ObservableCollection<CodeLensDataReferenceDetailsViewModel>(customData.Details.Select(details => new CodeLensDataReferenceDetailsViewModel(details)));

			m_entriesView = new ListCollectionView(m_detailEntries);
			m_entriesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(CodeLensDataReferenceDetailsViewModel.SourceFile)));

			m_entriesView.SortDescriptions.Add(new SortDescription(nameof(CodeLensDataReferenceDetailsViewModel.SourceFile), ListSortDirection.Ascending));
			m_entriesView.SortDescriptions.Add(new SortDescription(nameof(CodeLensDataReferenceDetailsViewModel.LineNumber), ListSortDirection.Ascending));
		}

		ObservableCollection<CodeLensDataReferenceDetailsViewModel> m_detailEntries = new ObservableCollection<CodeLensDataReferenceDetailsViewModel>();

		ListCollectionView m_entriesView;
		public ICollectionView EntriesView { get => m_entriesView; }
	}
}
