using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.FileTemplateCreator.ViewModels;
using GameCodersToolkit.FileTemplateCreator.Windows;
using GameCodersToolkit.SourceControl;
using GameCodersToolkit.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GameCodersToolkit.FileRenamer.ViewModels
{
	/// <summary>
	/// Shared helper that manages CMake file tree selection (CMake file -> Uber file -> Group)
	/// with support for adding new uber files and groups.
	/// Reused across MoveFiles, MoveFolder, and RenameFile dialogs.
	/// </summary>
	public partial class CCMakeSelectionHelper : ObservableObject
	{
		public void InitializeCMakeFileList()
		{
			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			if (config?.CreatorConfig?.CMakeFileEntries == null)
				return;

			CMakeFiles.Clear();

			foreach (var fileEntry in config.CreatorConfig.CMakeFileEntries)
			{
				CMakeFileViewModel makeFileViewModel = new CMakeFileViewModel();
				makeFileViewModel.ID = fileEntry.ID;
				makeFileViewModel.Name = fileEntry.ID;
				makeFileViewModel.OnSelect += OnCMakeFileSelected;
				CMakeFiles.Add(makeFileViewModel);
			}
		}

		private void OnCMakeFileSelected(CMakeFileViewModel selected)
		{
			foreach (var vm in CMakeFiles)
			{
				if (vm != selected)
				{
					vm.IsSelected = false;
				}
			}

			SelectedMakeFile = selected;
			LoadMakeFileContent();
		}

		private void LoadMakeFileContent()
		{
			MakeFileContent.Clear();
			SelectedUberFile = null;
			SelectedGroup = null;
			CurrentMakeFile = null;

			if (SelectedMakeFile == null)
				return;

			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			string makeFilePath = config?.FindMakeFilePathByID(SelectedMakeFile.ID);

			if (string.IsNullOrEmpty(makeFilePath) || !File.Exists(makeFilePath))
				return;

			IMakeFileParser parser = config.CreateParser();
			if (parser == null)
				return;

			CurrentMakeFile = parser.Parse(makeFilePath);
			if (CurrentMakeFile == null)
				return;

			foreach (IUberFileNode uberFile in CurrentMakeFile.GetUberFiles())
			{
				CMakeFileUberFileViewModel uberFileVm = new CMakeFileUberFileViewModel();
				uberFileVm.Node = uberFile;
				uberFileVm.Name = uberFile.GetName();
				uberFileVm.DisplayName = uberFileVm.Name + $" ({uberFile.GetGroups().Count()} Groups)";

				foreach (IGroupNode group in uberFile.GetGroups())
				{
					CMakeFileGroupViewModel groupVm = new CMakeFileGroupViewModel();
					groupVm.Node = group;
					groupVm.Name = group.GetName();
					groupVm.DisplayName = group.GetName() + $" ({group.GetFiles().Count()} Files)";
					groupVm.ParentUberFile = uberFileVm;

					foreach (IFileNode file in group.GetFiles())
					{
						CMakeFileFileViewModel fileVm = new CMakeFileFileViewModel();
						fileVm.Node = file;
						fileVm.Name = file.GetName();
						groupVm.Children.Add(fileVm);
					}

					uberFileVm.Children.Add(groupVm);
				}

				// Add "Add Group" button at the end of each uber file
				CMakeFileGroupViewModel lastGroupVm = uberFileVm.Children.OfType<CMakeFileGroupViewModel>().LastOrDefault();
				CSelectableEntryViewModel addGroupEntry = new CSelectableEntryViewModel();
				addGroupEntry.Name = "Add Group";
				addGroupEntry.OnSelect += (vm) => OnAddGroupFromTree(uberFileVm, lastGroupVm);
				uberFileVm.Children.Add(addGroupEntry);

				MakeFileContent.Add(uberFileVm);
			}

			// Add "Add Uber File" button at the end of the tree
			CMakeFileUberFileViewModel lastUberVm = MakeFileContent.OfType<CMakeFileUberFileViewModel>().LastOrDefault();
			CSelectableEntryViewModel addUberEntry = new CSelectableEntryViewModel();
			addUberEntry.Name = "Add Uber File";
			addUberEntry.OnSelect += (vm) => OnAddUberFileFromTree(lastUberVm);
			MakeFileContent.Add(addUberEntry);
		}

		/// <summary>
		/// Call this when a group is clicked in the tree. Toggles selection on/off.
		/// Automatically sets the parent uber file when a group is selected.
		/// </summary>
		public void ToggleGroupSelection(CMakeFileGroupViewModel groupVm)
		{
			if (groupVm == null)
				return;

			// If already selected, deselect
			if (SelectedGroup == groupVm)
			{
				groupVm.IsSelected = false;
				SelectedGroup = null;
				SelectedUberFile = null;
				return;
			}

			// Deselect previous group
			if (SelectedGroup != null)
			{
				SelectedGroup.IsSelected = false;
			}

			// Select new group
			groupVm.IsSelected = true;
			SelectedGroup = groupVm;

			// Auto-select the parent uber file
			if (groupVm.ParentUberFile != null)
			{
				if (SelectedUberFile != null)
					SelectedUberFile.IsSelected = false;

				groupVm.ParentUberFile.IsSelected = true;
				SelectedUberFile = groupVm.ParentUberFile;
			}
			else
			{
				// Find parent uber file by searching the tree
				foreach (var item in MakeFileContent)
				{
					if (item is CMakeFileUberFileViewModel parentUber)
					{
						foreach (var child in parentUber.Children)
						{
							if (child == groupVm)
							{
								if (SelectedUberFile != null)
									SelectedUberFile.IsSelected = false;

								parentUber.IsSelected = true;
								SelectedUberFile = parentUber;
								return;
							}
						}
					}
				}
			}
		}

		/// <summary>
		/// Call this when an uber file node is clicked in the tree.
		/// Selects the uber file and clears group selection.
		/// </summary>
		public void SelectUberFile(CMakeFileUberFileViewModel uberFileVm)
		{
			if (uberFileVm == null)
				return;

			// Deselect previous
			if (SelectedGroup != null)
			{
				SelectedGroup.IsSelected = false;
				SelectedGroup = null;
			}
			if (SelectedUberFile != null)
			{
				SelectedUberFile.IsSelected = false;
			}

			uberFileVm.IsSelected = true;
			SelectedUberFile = uberFileVm;
		}

		/// <summary>
		/// Clears all CMake selection state.
		/// </summary>
		public void ClearSelection()
		{
			if (SelectedGroup != null)
			{
				SelectedGroup.IsSelected = false;
				SelectedGroup = null;
			}
			if (SelectedUberFile != null)
			{
				SelectedUberFile.IsSelected = false;
				SelectedUberFile = null;
			}
		}

		/// <summary>
		/// Creates and inserts a new placeholder uber file VM into the tree.
		/// Inserts before the "Add Uber File" selectable entry if one exists.
		/// </summary>
		private void InsertNewUberFile(string name)
		{
			CMakeFileUberFileViewModel newUberVm = new CMakeFileUberFileViewModel();
			newUberVm.Name = name;
			newUberVm.DisplayName = name + " (0 Groups) [NEW]";
			newUberVm.IsNewEntry = true;

			// Add an "Add Group" button inside the new uber file
			CSelectableEntryViewModel addGroupEntry = new CSelectableEntryViewModel();
			addGroupEntry.Name = "Add Group";
			addGroupEntry.OnSelect += (vm) => OnAddGroupFromTree(newUberVm, null);
			newUberVm.Children.Add(addGroupEntry);

			// Insert before the "Add Uber File" selectable entry at the bottom
			int insertIndex = MakeFileContent.Count;
			for (int i = MakeFileContent.Count - 1; i >= 0; i--)
			{
				if (MakeFileContent[i] is CSelectableEntryViewModel)
				{
					insertIndex = i;
					break;
				}
			}

			MakeFileContent.Insert(insertIndex, newUberVm);
		}

		/// <summary>
		/// Creates and inserts a new placeholder group VM into the given uber file.
		/// Inserts before the "Add Group" selectable entry if one exists.
		/// </summary>
		private void InsertNewGroup(CMakeFileUberFileViewModel uberFileVm, string name)
		{
			CMakeFileGroupViewModel newGroupVm = new CMakeFileGroupViewModel();
			newGroupVm.Name = name;
			newGroupVm.DisplayName = name + " (0 Files) [NEW]";
			newGroupVm.IsNewEntry = true;
			newGroupVm.ParentUberFile = uberFileVm;

			// Insert before the "Add Group" selectable entry
			int insertIndex = uberFileVm.Children.Count;
			for (int i = uberFileVm.Children.Count - 1; i >= 0; i--)
			{
				if (uberFileVm.Children[i] is CSelectableEntryViewModel)
				{
					insertIndex = i;
					break;
				}
			}

			uberFileVm.Children.Insert(insertIndex, newGroupVm);
			uberFileVm.IsExpanded = true;

			// Update the uber file display name to reflect the new child count
			int groupCount = uberFileVm.Children.OfType<CMakeFileGroupViewModel>().Count();
			string baseName = uberFileVm.Name;
			string suffix = uberFileVm.IsNewEntry ? " [NEW]" : "";
			uberFileVm.DisplayName = $"{baseName} ({groupCount} Groups){suffix}";
		}

		/// <summary>
		/// Handler for the in-tree "Add Uber File" button.
		/// Opens a name dialog and creates a placeholder uber file.
		/// </summary>
		private Task OnAddUberFileFromTree(CMakeFileUberFileViewModel previousVm)
		{
			if (CurrentMakeFile == null)
				return Task.CompletedTask;

			Predicate<string> predicate = (result) =>
			{
				return !string.IsNullOrWhiteSpace(result)
					&& !MakeFileContent.OfType<CMakeFileUberFileViewModel>().Any(u => u.Name == result);
			};

			Action<string> errorAction = (result) =>
			{
				if (string.IsNullOrWhiteSpace(result))
					System.Windows.MessageBox.Show("Uber file name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				else
					System.Windows.MessageBox.Show($"There already is an uber file named {result}!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			};

			if (NameDialogWindow.ShowNameDialog("Enter Uber File name", out string newName, predicate, errorAction))
			{
				InsertNewUberFile(newName);
			}

			return Task.CompletedTask;
		}

		/// <summary>
		/// Handler for the in-tree "Add Group" button inside an uber file.
		/// Opens a name dialog and creates a placeholder group.
		/// </summary>
		private Task OnAddGroupFromTree(CMakeFileUberFileViewModel uberFileVm, CMakeFileGroupViewModel previousVm)
		{
			if (uberFileVm == null)
				return Task.CompletedTask;

			Predicate<string> predicate = (result) =>
			{
				return !string.IsNullOrWhiteSpace(result)
					&& !uberFileVm.Children.OfType<CMakeFileGroupViewModel>().Any(g => g.Name == result);
			};

			Action<string> errorAction = (result) =>
			{
				if (string.IsNullOrWhiteSpace(result))
					System.Windows.MessageBox.Show("Group name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				else
					System.Windows.MessageBox.Show($"There already is a group named {result}!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
			};

			if (NameDialogWindow.ShowNameDialog("Enter Group name", out string newName, predicate, errorAction))
			{
				InsertNewGroup(uberFileVm, newName);
			}

			return Task.CompletedTask;
		}

		[RelayCommand]
		private void AddNewUberFile()
		{
			if (CurrentMakeFile == null || string.IsNullOrWhiteSpace(NewUberFileName))
				return;

			InsertNewUberFile(NewUberFileName.Trim());
			NewUberFileName = string.Empty;
		}

		[RelayCommand]
		private void AddNewGroup()
		{
			if (SelectedUberFile == null || string.IsNullOrWhiteSpace(NewGroupName))
				return;

			InsertNewGroup(SelectedUberFile, NewGroupName.Trim());
			NewGroupName = string.Empty;
		}

		/// <summary>
		/// Applies the selected CMake location (uber file + group) to the CMake file by adding the given file paths.
		/// Call this during the actual move/rename execution to persist the new entries.
		/// </summary>
		public async Task<bool> AddFilesToCMakeLocationAsync(
			Dictionary<string, string> moveMap,
			ObservableCollection<CRenameResultViewModel> results)
		{
			if (CurrentMakeFile == null || SelectedUberFile == null || SelectedGroup == null)
				return false;

			try
			{
				string makeFilePath = CurrentMakeFile.GetOriginalFilePath();

				// Re-parse the CMake file from disk to pick up any changes made by
				// prior operations (e.g. RemoveFilesFromOldCMakeLocationsAsync).
				// Without this, we'd overwrite removal changes with stale data.
				// Run on a background thread to avoid blocking the UI.
				CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
				IMakeFileParser parser = config?.CreateParser();
				if (parser != null)
				{
					IMakeFile freshMakeFile = await Task.Run(() => parser.Parse(makeFilePath));
					if (freshMakeFile != null)
					{
						CurrentMakeFile = freshMakeFile;

						// Re-find the selected uber file and group nodes in the fresh parse
						if (!SelectedUberFile.IsNewEntry && SelectedUberFile.Node != null)
						{
							SelectedUberFile.Node = CurrentMakeFile.GetUberFiles()
								.FirstOrDefault(u => u.GetName() == SelectedUberFile.Name);
						}

						if (!SelectedGroup.IsNewEntry && SelectedGroup.Node != null && SelectedUberFile.Node != null)
						{
							SelectedGroup.Node = SelectedUberFile.Node.GetGroups()
								.FirstOrDefault(g => g.GetName() == SelectedGroup.Name);
						}
					}
				}

				// Step 1: If the uber file is new, add it first
				if (SelectedUberFile.IsNewEntry)
				{
					IUberFileNode lastUberNode = CurrentMakeFile.GetUberFiles().LastOrDefault();
					CurrentMakeFile = await CurrentMakeFile.AddUberFileAsync(lastUberNode, SelectedUberFile.Name);

					// Re-find the newly created uber file node
					SelectedUberFile.Node = CurrentMakeFile.GetUberFiles()
						.FirstOrDefault(u => u.GetName() == SelectedUberFile.Name);

					if (SelectedUberFile.Node == null)
					{
						results.Add(new CRenameResultViewModel
						{
							Description = $"Failed to add new uber file '{SelectedUberFile.Name}' to CMake file.",
							IsSuccess = false
						});
						return false;
					}
				}

				// Step 2: If the group is new, add it
				if (SelectedGroup.IsNewEntry)
				{
					IGroupNode lastGroup = SelectedUberFile.Node?.GetGroups().LastOrDefault();
					CurrentMakeFile = await CurrentMakeFile.AddGroupAsync(SelectedUberFile.Node, lastGroup, SelectedGroup.Name);

					// Re-find nodes after re-parse
					SelectedUberFile.Node = CurrentMakeFile.GetUberFiles()
						.FirstOrDefault(u => u.GetName() == SelectedUberFile.Name);
					SelectedGroup.Node = SelectedUberFile.Node?.GetGroups()
						.FirstOrDefault(g => g.GetName() == SelectedGroup.Name);

					if (SelectedGroup.Node == null)
					{
						results.Add(new CRenameResultViewModel
						{
							Description = $"Failed to add new group '{SelectedGroup.Name}' to uber file '{SelectedUberFile.Name}'.",
							IsSuccess = false
						});
						return false;
					}
				}

				// Step 3: Add the file entries
				List<string> newRelativePaths = new List<string>();
				foreach (var pair in moveMap)
				{
					string relativePath = makeFilePath.MakeRelativePath(pair.Value);
					newRelativePaths.Add(relativePath);
				}

				IFileNode lastFile = SelectedGroup.Node.GetFiles().LastOrDefault();

				await PerforceConnection.TryCheckoutFilesAsync(new string[] { makeFilePath });
				CurrentMakeFile = await CurrentMakeFile.AddFilesAsync(SelectedUberFile.Node, SelectedGroup.Node, lastFile, newRelativePaths);
				await CurrentMakeFile.SaveAsync();

				results.Add(new CRenameResultViewModel
				{
					Description = $"Added {newRelativePaths.Count} file(s) to {SelectedUberFile.Name} / {SelectedGroup.Name}",
					IsSuccess = true
				});

				return true;
			}
			catch (Exception ex)
			{
				results.Add(new CRenameResultViewModel
				{
					Description = $"Failed to add files to CMake location: {ex.Message}",
					IsSuccess = false
				});
				return false;
			}
		}

		/// <summary>
		/// Whether the user has made a complete CMake location selection (uber file + group).
		/// </summary>
		public bool HasValidSelection => SelectedMakeFile != null && SelectedUberFile != null && SelectedGroup != null;

		// Properties
		private ObservableCollection<CMakeFileViewModel> m_cmakeFiles = new ObservableCollection<CMakeFileViewModel>();
		public ObservableCollection<CMakeFileViewModel> CMakeFiles { get => m_cmakeFiles; set => SetProperty(ref m_cmakeFiles, value); }

		private ObservableCollection<object> m_makeFileContent = new ObservableCollection<object>();
		public ObservableCollection<object> MakeFileContent { get => m_makeFileContent; set => SetProperty(ref m_makeFileContent, value); }

		private CMakeFileViewModel m_selectedMakeFile;
		public CMakeFileViewModel SelectedMakeFile { get => m_selectedMakeFile; set => SetProperty(ref m_selectedMakeFile, value); }

		private CMakeFileUberFileViewModel m_selectedUberFile;
		public CMakeFileUberFileViewModel SelectedUberFile { get => m_selectedUberFile; set => SetProperty(ref m_selectedUberFile, value); }

		private CMakeFileGroupViewModel m_selectedGroup;
		public CMakeFileGroupViewModel SelectedGroup { get => m_selectedGroup; set => SetProperty(ref m_selectedGroup, value); }

		private string m_newUberFileName = string.Empty;
		public string NewUberFileName { get => m_newUberFileName; set => SetProperty(ref m_newUberFileName, value); }

		private string m_newGroupName = string.Empty;
		public string NewGroupName { get => m_newGroupName; set => SetProperty(ref m_newGroupName, value); }

		private string m_selectionSummary = "No CMake location selected";
		public string SelectionSummary { get => m_selectionSummary; set => SetProperty(ref m_selectionSummary, value); }

		public IMakeFile CurrentMakeFile { get; set; }
	}
}
