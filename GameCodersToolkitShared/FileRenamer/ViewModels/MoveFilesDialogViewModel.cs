using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.FileTemplateCreator.ViewModels;
using GameCodersToolkit.SourceControl;
using GameCodersToolkit.Utils;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace GameCodersToolkit.FileRenamer.ViewModels
{
	public partial class CMoveFilesDialogViewModel : ObservableObject
	{
		public CMoveFilesDialogViewModel()
		{
			WindowTitle = "Move File(s)";
		}

		[RelayCommand]
		private void AddFiles()
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Multiselect = true;
			openFileDialog.Title = "Select file(s) to move";
			openFileDialog.Filter = FileOperationHelper.SourceFileDialogFilter;

			if (!string.IsNullOrEmpty(TargetDirectory) && Directory.Exists(TargetDirectory))
			{
				openFileDialog.InitialDirectory = TargetDirectory;
			}

			if (openFileDialog.ShowDialog() == true)
			{
				foreach (string filePath in openFileDialog.FileNames)
				{
					if (!SelectedFiles.Any(f => string.Equals(f.FilePath, filePath, StringComparison.OrdinalIgnoreCase)))
					{
						SelectedFiles.Add(new CRelatedFileViewModel
						{
							FilePath = filePath,
							IsSelected = true
						});
					}
				}

				// Auto-fill target directory from first file if not set
				if (string.IsNullOrEmpty(TargetDirectory) && SelectedFiles.Count > 0)
				{
					TargetDirectory = Path.GetDirectoryName(SelectedFiles[0].FilePath);
				}
			}
		}

		[RelayCommand]
		private void RemoveSelectedFiles()
		{
			var filesToRemove = SelectedFiles.Where(f => !f.IsSelected).ToList();
			foreach (var file in filesToRemove)
			{
				SelectedFiles.Remove(file);
			}
		}

		[RelayCommand]
		private void ClearFiles()
		{
			SelectedFiles.Clear();
		}

		[RelayCommand]
		private void BrowseForTargetFolder()
		{
			string initialDir = !string.IsNullOrEmpty(TargetDirectory) ? TargetDirectory : null;
			string selectedPath = ModernFolderPicker.ShowDialog(initialDir, "Select target folder for the file(s)");
			if (!string.IsNullOrEmpty(selectedPath))
			{
				TargetDirectory = selectedPath;
			}
		}

		private bool Validate()
		{
			ErrorMessage = string.Empty;

			if (SelectedFiles.Count == 0)
			{
				ErrorMessage = "No files selected. Use 'Add Files...' to select files to move.";
				return false;
			}

			var filesToMove = SelectedFiles.Where(f => f.IsSelected).ToList();
			if (filesToMove.Count == 0)
			{
				ErrorMessage = "No files are checked for moving.";
				return false;
			}

			if (string.IsNullOrWhiteSpace(TargetDirectory))
			{
				ErrorMessage = "Target directory is not set.";
				return false;
			}

			if (!Directory.Exists(TargetDirectory))
			{
				ErrorMessage = $"Target directory does not exist: {TargetDirectory}";
				return false;
			}

			// Check if all files are already in the target directory
			bool allSameDir = filesToMove.All(f =>
			{
				string fileDir = Path.GetFullPath(Path.GetDirectoryName(f.FilePath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string targetDir = Path.GetFullPath(TargetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				return string.Equals(fileDir, targetDir, StringComparison.OrdinalIgnoreCase);
			});

			if (allSameDir)
			{
				ErrorMessage = "All selected files are already in the target directory.";
				return false;
			}

			// Check for conflicts
			List<string> conflicts = new List<string>();
			foreach (var file in filesToMove)
			{
				string newPath = Path.Combine(TargetDirectory, Path.GetFileName(file.FilePath));
				if (File.Exists(newPath) && !string.Equals(Path.GetFullPath(file.FilePath), Path.GetFullPath(newPath), StringComparison.OrdinalIgnoreCase))
				{
					conflicts.Add(Path.GetFileName(file.FilePath));
				}
			}

			if (conflicts.Count > 0)
			{
				ErrorMessage = $"File(s) already exist in target: {string.Join(", ", conflicts)}";
				return false;
			}

			return true;
		}

		[RelayCommand]
		private async Task MoveAsync()
		{
			if (!Validate())
				return;

			List<CRelatedFileViewModel> filesToMove = SelectedFiles.Where(f => f.IsSelected).ToList();

			// Pre-check Perforce
			if (!await FileOperationHelper.CheckPerforceAndConfirmAsync(filesToMove.Select(f => f.FilePath)))
				return;

			IsMoving = true;
			MoveResults.Clear();
			ProgressMessage = "Preparing...";

			try
			{
				string targetDir = Path.GetFullPath(TargetDirectory);

				// Build the move map: old path -> new path
				Dictionary<string, string> moveMap = new Dictionary<string, string>();
				foreach (var file in filesToMove)
				{
					string newPath = Path.Combine(targetDir, Path.GetFileName(file.FilePath));
					moveMap[file.FilePath] = newPath;
				}

				// Step 1: Update CMakeLists.txt files
				ProgressMessage = "Updating CMakeLists.txt files...";
				await FileOperationHelper.UpdateCMakeFilesAsync(moveMap, MoveResults);

				// Step 2: If user selected a new CMake file + uber + group, add entries there
				if (SelectedMakeFile != null && SelectedUberFile != null && SelectedGroup != null)
				{
					ProgressMessage = "Adding files to new CMake location...";
					await AddFilesToNewCMakeLocationAsync(moveMap);
				}

				// Step 3: Update #include references
				ProgressMessage = "Searching for #include references...";
				string searchRoot = filesToMove.Count > 0
					? Path.GetDirectoryName(filesToMove[0].FilePath)
					: targetDir;
				await FileOperationHelper.UpdateIncludeReferencesAsync(
					moveMap, searchRoot, MoveResults, msg => ProgressMessage = msg);

				// Step 4: Move the actual files on disk
				ProgressMessage = "Moving files on disk...";
				await FileOperationHelper.MoveFilesOnDiskAsync(moveMap, MoveResults);

				MoveResults.Add(new CRenameResultViewModel
				{
					Description = "Move completed successfully!",
					IsSuccess = true
				});

				HasCompleted = true;
			}
			catch (Exception ex)
			{
				MoveResults.Add(new CRenameResultViewModel
				{
					Description = $"Error during move: {ex.Message}",
					IsSuccess = false
				});

				Community.VisualStudio.Toolkit.MessageBox errorMessageBox = new Community.VisualStudio.Toolkit.MessageBox();
				errorMessageBox.ShowError($"[MoveFiles] Exception during file move operation: {ex.Message}!");
			}
			finally
			{
				IsMoving = false;
				ProgressMessage = string.Empty;
			}
		}

		private async Task AddFilesToNewCMakeLocationAsync(Dictionary<string, string> moveMap)
		{
			try
			{
				if (CurrentMakeFile == null || SelectedUberFile == null || SelectedGroup == null)
					return;

				List<string> newRelativePaths = new List<string>();
				string makeFilePath = CurrentMakeFile.GetOriginalFilePath();

				foreach (var pair in moveMap)
				{
					string relativePath = makeFilePath.MakeRelativePath(pair.Value);
					newRelativePaths.Add(relativePath);
				}

				// Find the last file in the group to insert after
				IFileNode lastFile = SelectedGroup.Node.GetFiles().LastOrDefault();

				await PerforceConnection.TryCheckoutFilesAsync(new string[] { makeFilePath });
				CurrentMakeFile = await CurrentMakeFile.AddFilesAsync(SelectedUberFile.Node, SelectedGroup.Node, lastFile, newRelativePaths);
				await CurrentMakeFile.SaveAsync();

				MoveResults.Add(new CRenameResultViewModel
				{
					Description = $"Added {newRelativePaths.Count} file(s) to {SelectedUberFile.Name} / {SelectedGroup.Name}",
					IsSuccess = true
				});
			}
			catch (Exception ex)
			{
				MoveResults.Add(new CRenameResultViewModel
				{
					Description = $"Failed to add files to CMake location: {ex.Message}",
					IsSuccess = false
				});
			}
		}

		#region CMake / Uber / Group Selection

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

					foreach (IFileNode file in group.GetFiles())
					{
						CMakeFileFileViewModel fileVm = new CMakeFileFileViewModel();
						fileVm.Node = file;
						fileVm.Name = file.GetName();
						groupVm.Children.Add(fileVm);
					}

					uberFileVm.Children.Add(groupVm);
				}

				MakeFileContent.Add(uberFileVm);
			}
		}

		#endregion

		[RelayCommand]
		private void Close()
		{
			OnRequestClose?.Invoke(this, EventArgs.Empty);
		}

		// Properties
		private string m_windowTitle = "Move File(s)";
		public string WindowTitle { get => m_windowTitle; set => SetProperty(ref m_windowTitle, value); }

		private string m_targetDirectory = string.Empty;
		public string TargetDirectory { get => m_targetDirectory; set => SetProperty(ref m_targetDirectory, value); }

		private string m_errorMessage = string.Empty;
		public string ErrorMessage { get => m_errorMessage; set => SetProperty(ref m_errorMessage, value); }

		private bool m_isMoving = false;
		public bool IsMoving { get => m_isMoving; set => SetProperty(ref m_isMoving, value); }

		private bool m_hasCompleted = false;
		public bool HasCompleted { get => m_hasCompleted; set => SetProperty(ref m_hasCompleted, value); }

		private string m_progressMessage = string.Empty;
		public string ProgressMessage { get => m_progressMessage; set => SetProperty(ref m_progressMessage, value); }

		private ObservableCollection<CRelatedFileViewModel> m_selectedFiles = new ObservableCollection<CRelatedFileViewModel>();
		public ObservableCollection<CRelatedFileViewModel> SelectedFiles { get => m_selectedFiles; set => SetProperty(ref m_selectedFiles, value); }

		private ObservableCollection<CRenameResultViewModel> m_moveResults = new ObservableCollection<CRenameResultViewModel>();
		public ObservableCollection<CRenameResultViewModel> MoveResults { get => m_moveResults; set => SetProperty(ref m_moveResults, value); }

		// CMake selection properties
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

		private IMakeFile CurrentMakeFile { get; set; }

		public event EventHandler OnRequestClose;
	}
}
