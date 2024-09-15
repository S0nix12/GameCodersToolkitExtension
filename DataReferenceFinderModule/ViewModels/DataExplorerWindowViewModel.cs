using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.DataReferenceFinderModule;
using GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using Microsoft.VisualStudio.Shell.Internal.FileEnumerationService;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace GameCodersToolkit.DataReferenceFinderModule.ViewModels
{
	public class DataExplorerSubTypeViewModel : ObservableObject
	{
		public DataExplorerSubTypeViewModel(string inName)
		{
			Name = inName;

			m_dataEntriesView = new ListCollectionView(DataEntries);
			DataEntriesView.Filter = (object entry) =>
			{
				DataEntryViewModel dataEntry = entry as DataEntryViewModel;
				return searchTokens.Length == 0 
				|| searchTokens.All(token => dataEntry.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
			};
		}

		public void SetSearchFilter(string[] inSearchTokens)
		{
			searchTokens = inSearchTokens;
			DataEntriesView.Refresh();
		}

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }
		string[] searchTokens = { };
		public string Name { get; private set; }
		public ObservableCollection<DataEntryViewModel> DataEntries { get; private set; } = new ObservableCollection<DataEntryViewModel>();
		private ListCollectionView m_dataEntriesView;
		public ICollectionView DataEntriesView { get => m_dataEntriesView; }
	}

	public class DataExplorerFileViewModel : ObservableObject
	{
		public DataExplorerFileViewModel(string filePath)
		{
			FilePath = Path.GetFileName(filePath);
			if (GameCodersToolkitPackage.ReferenceDatabase.EntriesPerFile.TryGetValue(filePath, out var dataEntries))
			{
				foreach (var dataEntry in dataEntries)
				{
					DataExplorerSubTypeViewModel subTypeVM = Entries.FirstOrDefault(subEntry => subEntry.Name == dataEntry.SubType);
					if (subTypeVM == null)
					{
						subTypeVM = new DataExplorerSubTypeViewModel(dataEntry.SubType);
						Entries.Add(subTypeVM);
					}
					subTypeVM.DataEntries.Add(new DataEntryViewModel(dataEntry));
				}
			}

			m_entriesView = new ListCollectionView(Entries);
			EntriesView.Filter = (object entry) =>
			{
				DataExplorerSubTypeViewModel subTypeEntry = entry as DataExplorerSubTypeViewModel;
				return searchTokens.Length == 0
				|| !subTypeEntry.DataEntriesView.IsEmpty
				|| searchTokens.All(token => subTypeEntry.Name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
			};
		}

		public void SetSearchFilter(string[] inSearchTokens)
		{
			searchTokens = inSearchTokens;
			foreach (var entry in Entries)
			{
				entry.SetSearchFilter(searchTokens);
			}
			EntriesView.Refresh();
		}

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }
		string[] searchTokens = { };
		public string FilePath { get; private set; }
		public ObservableCollection<DataExplorerSubTypeViewModel> Entries { get; private set; } = new ObservableCollection<DataExplorerSubTypeViewModel>();

		private ListCollectionView m_entriesView;
		public ICollectionView EntriesView { get => m_entriesView; }
	}

	public partial class DataExplorerWindowViewModel : ObservableObject
	{
		public DataExplorerWindowViewModel()
		{
			PopulateEntries();
			m_fileEntriesView = new ListCollectionView(FileEntries);
			FileEntriesView.Filter = (object entry) =>
			{
				DataExplorerFileViewModel fileEntry = entry as DataExplorerFileViewModel;
				return searchTokens.Length == 0
				|| !fileEntry.EntriesView.IsEmpty
				|| searchTokens.All(token => fileEntry.FilePath.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
			};
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
			searchTokens = m_searchFilter.Split(' ');
			foreach (var fileEntry in FileEntries)
			{
				fileEntry.SetSearchFilter(searchTokens);
			}
			FileEntriesView.Refresh();
		}

		string[] searchTokens = { };
		string m_searchFilter = "Search...";
		public string SearchFilter { get => m_searchFilter; set { SetProperty(ref m_searchFilter, value); OnSearchFilterUpdated(); } }
		public ObservableCollection<DataExplorerFileViewModel> FileEntries { get; private set; } = new ObservableCollection<DataExplorerFileViewModel>();
		private ListCollectionView m_fileEntriesView;
		public ICollectionView FileEntriesView { get => m_fileEntriesView; }
	}
}
