using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.DataReferenceFinderModule;
using GameCodersToolkit.DataReferenceFinderModule.DataEditorCommunication;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.Utils;
using Microsoft.VisualStudio.Shell.Internal.FileEnumerationService;
using Microsoft.VisualStudio.Text;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Navigation;

namespace GameCodersToolkit.DataReferenceFinderModule.ViewModels
{
	public class DataExplorerSubTypeViewModel : ObservableObject, INestedSearchableViewModel
	{
		public DataExplorerSubTypeViewModel(string inName)
		{
			Name = inName;

			m_dataEntriesView = new ListCollectionView(DataEntries);
		}

		public string GetSearchField()
		{
			return Name;
		}

		public void SetSelectedTypeFilter(string typeFilter)
		{
			if (m_selectedTypeFilter == typeFilter)
				return;

			m_selectedTypeFilter = typeFilter;
			if (string.IsNullOrWhiteSpace(m_selectedTypeFilter))
			{
				HasAnyChildsMatchType = true;
			}
			else
			{
				HasAnyChildsMatchType = false;
				foreach (var entryVM in DataEntries)
				{
					if (entryVM.SourceEntry != null && entryVM.SourceEntry.BaseType.Equals(typeFilter, StringComparison.OrdinalIgnoreCase))
					{
						HasAnyChildsMatchType = true;
						break;
					}
				}
			}

			DataEntriesView.Refresh();
		}

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }
		public string Name { get; private set; }
		public ObservableCollection<DataEntryViewModel> DataEntries { get; private set; } = new ObservableCollection<DataEntryViewModel>();
		private ListCollectionView m_dataEntriesView;
		public ICollectionView DataEntriesView { get => m_dataEntriesView; }

		string[] m_searchTokens = { };

		private string m_selectedTypeFilter = "";
		public bool HasAnyChildsMatchType { get; private set; } = true;

		// INestedSearchableViewModel
		public Predicate<object> AdditionalFilter => (object entry) =>
		{
			if (string.IsNullOrWhiteSpace(m_selectedTypeFilter))
				return true;

			DataEntryViewModel entryVM = entry as DataEntryViewModel;
			return entryVM.SourceEntry != null && entryVM.SourceEntry.BaseType.Equals(m_selectedTypeFilter, StringComparison.OrdinalIgnoreCase);
		};
		public string[] SearchTokens { get => m_searchTokens; set => m_searchTokens = value; }
		public IEnumerable ChildEntries => DataEntries;
		public ICollectionView FilteredView => DataEntriesView;
		// ~INestedSearchableViewModel
	}

	public class DataExplorerFileViewModel : ObservableObject, INestedSearchableViewModel
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
		}

		public void SetSelectedTypeFilter(string typeFilter)
		{
			if (m_selectedTypeFilter == typeFilter)
				return;

			m_selectedTypeFilter = typeFilter;
			HasAnyChildsMatchType = string.IsNullOrWhiteSpace(m_selectedTypeFilter);

			foreach (var entry in Entries)
			{
				entry.SetSelectedTypeFilter(typeFilter);
				HasAnyChildsMatchType |= entry.HasAnyChildsMatchType;
			}

			EntriesView.Refresh();
		}

		public string GetSearchField()
		{
			return FilePath;
		}

		private bool m_isExpanded;
		public bool IsExpanded { get => m_isExpanded; set => SetProperty(ref m_isExpanded, value); }
		public string FilePath { get; private set; }
		public ObservableCollection<DataExplorerSubTypeViewModel> Entries { get; private set; } = new ObservableCollection<DataExplorerSubTypeViewModel>();

		private ListCollectionView m_entriesView;
		public ICollectionView EntriesView { get => m_entriesView; }

		private string m_selectedTypeFilter = "";
		public bool HasAnyChildsMatchType { get; private set; } = true;

		string[] m_searchTokens = { };
		// INestedSearchableViewModel
		public Predicate<object> AdditionalFilter => (entry) => ((DataExplorerSubTypeViewModel)entry).HasAnyChildsMatchType;
		public string[] SearchTokens { get => m_searchTokens; set => m_searchTokens = value; }
		public IEnumerable ChildEntries => Entries;
		public ICollectionView FilteredView => EntriesView;
		// ~INestedSearchableViewModel
	}

	public partial class DataExplorerWindowViewModel : ObservableObject
	{
		public DataExplorerWindowViewModel()
		{
			PopulateEntries();
			GameCodersToolkitPackage.DataLocationsConfig.ConfigLoaded += OnReferenceFinderConfigLoaded;
			m_fileEntriesView = new ListCollectionView(FileEntries);
			FileEntriesView.Filter = (object entry) =>
			{
				DataExplorerFileViewModel entryVM = entry as DataExplorerFileViewModel;
				if (!entryVM.HasAnyChildsMatchType)
					return false;

				if (entry is ISearchableViewModel searchableEntry)
				{
					return SearchEntryUtils.FilterChild(searchableEntry, searchTokens);
				}
				return false;
			};
			GameCodersToolkitPackage.ReferenceDatabase.DatabaseUpdated += OnDatabaseUpdated;
			UpdatePossibleDataTypes();
		}

		[RelayCommand]
		void Refresh()
		{
			Task.Run(async delegate
			{
				GameCodersToolkitPackage.ReferenceDatabase.ClearDatabase();
				await GameCodersToolkitPackage.DataParsingEngine.StartDataParseAsync();
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				//PopulateEntries();
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
				SearchEntryUtils.SetSearchTokens(fileEntry, searchTokens);
			}
			FileEntriesView.Refresh();
		}

		void OnSelectedTypeFilterUpdated()
		{
			if (m_selectedTypeFilter == "All")
				m_selectedTypeFilter = "";

			foreach (var entryVM in FileEntries)
			{
				entryVM.SetSelectedTypeFilter(m_selectedTypeFilter);
			}
			FileEntriesView.Refresh();
		}

		async void OnReferenceFinderConfigLoaded(object sender, EventArgs e)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			UpdatePossibleDataTypes();
		}

		void UpdatePossibleDataTypes()
		{
			PossibleDataTypes.Clear();
			PossibleDataTypes.Add("All");
			var parsingDescriptions = GameCodersToolkitPackage.DataLocationsConfig.GetParsingDescriptions();
			foreach (var parsingDescription in parsingDescriptions)
			{
				string typeName = parsingDescription.TypeName ?? parsingDescription.Name;
				if (!PossibleDataTypes.Contains(typeName))
				{
					PossibleDataTypes.Add(typeName);
				}
			}
			SelectedTypeFilter = "All";
		}

		private void OnDatabaseUpdated(object sender, DatabaseUpdatedEventArgs e)
		{
			ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				switch (e.UpdateType)
				{
					case EDatabseUpdateEvent.EntriesAdded:
						var newEntry = new DataExplorerFileViewModel(e.FilePath);
						SearchEntryUtils.SetSearchTokens(newEntry, searchTokens);
						FileEntries.Add(newEntry);
						break;
					case EDatabseUpdateEvent.EntriesRemoved:
						FileEntries.RemoveAll(entry => entry.FilePath == e.FilePath);
						break;
					case EDatabseUpdateEvent.DatabaseCleared:
						FileEntries.Clear();
						break;
				}
			}).FireAndForget();
		}

		string[] searchTokens = { };
		string m_searchFilter = "Search...";
		public string SearchFilter { get => m_searchFilter; set { SetProperty(ref m_searchFilter, value); OnSearchFilterUpdated(); } }

		string m_selectedTypeFilter = "";
		public string SelectedTypeFilter { get => string.IsNullOrWhiteSpace(m_selectedTypeFilter) ? "All" : m_selectedTypeFilter; set { SetProperty(ref m_selectedTypeFilter, value); OnSelectedTypeFilterUpdated(); } }
		public ObservableCollection<DataExplorerFileViewModel> FileEntries { get; private set; } = new ObservableCollection<DataExplorerFileViewModel>();
		private ListCollectionView m_fileEntriesView;
		public ICollectionView FileEntriesView { get => m_fileEntriesView; }

		public ObservableCollection<string> PossibleDataTypes { get; private set; } = new ObservableCollection<string>();
	}
}
