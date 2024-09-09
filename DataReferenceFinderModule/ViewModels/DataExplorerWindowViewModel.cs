using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameCodersToolkit.DataReferenceFinderModule.ViewModels
{
	public partial class DataExplorerEntryViewModel : ObservableObject
	{
		public DataExplorerEntryViewModel(DataEntry sourceEntry)
		{
			m_sourceEntry = sourceEntry;
		}

		[RelayCommand]
		private void FindReferences()
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate { await ReferenceDatabaseUtils.ExecuteFindOperationOnDatabaseAsync(m_sourceEntry.Identifier, Name); });
		}

		private DataEntry m_sourceEntry;
		public string Name { get => m_sourceEntry.Name; }
		public int LineNumber { get => m_sourceEntry.SourceLineNumber; }
	}

	public class DataExplorerFileViewModel : ObservableObject
	{
		public DataExplorerFileViewModel(string filePath)
		{
			FilePath = Path.GetFileName(filePath);
			if (GameCodersToolkitPackage.ReferenceDatabase.EntriesPerFile.TryGetValue(filePath, out var entries))
			{
				foreach (var entry in entries)
				{
					DataEntries.Add(new DataExplorerEntryViewModel(entry));
				}
			}
		}

		public string FilePath { get; private set; }
		public ObservableCollection<DataExplorerEntryViewModel> DataEntries { get; private set; } = new ObservableCollection<DataExplorerEntryViewModel>();
	}

	public partial class DataExplorerWindowViewModel : ObservableObject
	{
		public DataExplorerWindowViewModel()
		{
			PopulateEntries();
		}

		[RelayCommand]
		void Refresh()
		{
			Task.Run(async delegate
			{
				await GameCodersToolkitPackage.DataParsingEngine.StartDataParseAsync();
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				PopulateEntries();
			}).FireAndForget();
		}

		void PopulateEntries()
		{
			FileEntries.Clear();
			foreach (var fileEntry in GameCodersToolkitPackage.ReferenceDatabase.EntriesPerFile)
			{
				FileEntries.Add(new DataExplorerFileViewModel(fileEntry.Key));
			}
		}

		void OnSearchFilterUpdated()
		{

		}

		string m_searchFilter = "Search...";
		public string SearchFilter { get => m_searchFilter; set { SetProperty(ref m_searchFilter, value); OnSearchFilterUpdated(); } }
		public ObservableCollection<DataExplorerFileViewModel> FileEntries { get; private set; } = new ObservableCollection<DataExplorerFileViewModel>();
	}
}
