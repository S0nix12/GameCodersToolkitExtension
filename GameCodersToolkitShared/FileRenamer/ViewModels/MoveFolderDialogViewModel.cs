using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
	public partial class CMoveFolderDialogViewModel : ObservableObject
	{
		public CMoveFolderDialogViewModel()
		{
			WindowTitle = "Move Folder";
			CMakeSelection.InitializeCMakeFileList();
		}

		[RelayCommand]
		private void BrowseForSourceFolder()
		{
			string initialDir = !string.IsNullOrEmpty(SourceDirectory) ? SourceDirectory : null;
			string selectedPath = ModernFolderPicker.ShowDialog(initialDir, "Select source folder to move");
			if (!string.IsNullOrEmpty(selectedPath))
			{
				SourceDirectory = selectedPath;
				DiscoverFiles();
			}
		}

		[RelayCommand]
		private void BrowseForTargetFolder()
		{
			string initialDir = !string.IsNullOrEmpty(TargetDirectory) ? TargetDirectory : null;
			string selectedPath = ModernFolderPicker.ShowDialog(initialDir, "Select target folder");
			if (!string.IsNullOrEmpty(selectedPath))
			{
				TargetDirectory = selectedPath;
			}
		}

		private void DiscoverFiles()
		{
			DiscoveredFiles.Clear();

			if (string.IsNullOrEmpty(SourceDirectory) || !Directory.Exists(SourceDirectory))
				return;

			foreach (string ext in FileOperationHelper.SourceExtensions)
			{
				string pattern = "*" + ext;
				foreach (string filePath in Directory.GetFiles(SourceDirectory, pattern, SearchOption.AllDirectories))
				{
					DiscoveredFiles.Add(new CRelatedFileViewModel
					{
						FilePath = filePath,
						IsSelected = true
					});
				}
			}
		}

		[RelayCommand]
		private void RefreshFiles()
		{
			DiscoverFiles();
		}

		private bool Validate()
		{
			ErrorMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(SourceDirectory))
			{
				ErrorMessage = "Source directory is not set.";
				return false;
			}

			if (!Directory.Exists(SourceDirectory))
			{
				ErrorMessage = $"Source directory does not exist: {SourceDirectory}";
				return false;
			}

			if (string.IsNullOrWhiteSpace(TargetDirectory))
			{
				ErrorMessage = "Target directory is not set.";
				return false;
			}

			if (!Directory.Exists(TargetDirectory))
			{
				var result = System.Windows.MessageBox.Show(
					$"Target directory does not exist:\n{TargetDirectory}\n\nDo you want to create it?",
					"Create Directory?",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return false;

				try
				{
					Directory.CreateDirectory(TargetDirectory);
				}
				catch (Exception ex)
				{
					ErrorMessage = $"Failed to create directory: {ex.Message}";
					return false;
				}
			}

			string normalizedSource = Path.GetFullPath(SourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string normalizedTarget = Path.GetFullPath(TargetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
			{
				ErrorMessage = "Source and target directories are the same.";
				return false;
			}

			var filesToMove = DiscoveredFiles.Where(f => f.IsSelected).ToList();
			if (filesToMove.Count == 0)
			{
				ErrorMessage = "No files found or checked for moving.";
				return false;
			}

			// Check for conflicts in the target directory
			List<string> conflicts = new List<string>();
			foreach (var file in filesToMove)
			{
				// Compute relative path from source dir to file, then combine with target dir
				string relativePath = file.FilePath.Substring(normalizedSource.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string newPath = Path.Combine(normalizedTarget, relativePath);
				if (File.Exists(newPath))
				{
					conflicts.Add(relativePath);
				}
			}

			if (conflicts.Count > 0)
			{
				string conflictList = conflicts.Count > 5
					? string.Join(", ", conflicts.Take(5)) + $" ... and {conflicts.Count - 5} more"
					: string.Join(", ", conflicts);
				ErrorMessage = $"File(s) already exist in target: {conflictList}";
				return false;
			}

			return true;
		}

		[RelayCommand]
		private async Task MoveAsync()
		{
			if (!Validate())
				return;

			List<CRelatedFileViewModel> filesToMove = DiscoveredFiles.Where(f => f.IsSelected).ToList();

			// Pre-check Perforce
			if (!await FileOperationHelper.CheckPerforceAndConfirmAsync(filesToMove.Select(f => f.FilePath)))
				return;

			IsMoving = true;
			MoveResults.Clear();
			ProgressMessage = "Preparing...";

			try
			{
				string sourceDir = Path.GetFullPath(SourceDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				string targetDir = Path.GetFullPath(TargetDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

				// Build move map preserving folder structure relative to source
				Dictionary<string, string> moveMap = new Dictionary<string, string>();
				foreach (var file in filesToMove)
				{
					string relativePath = file.FilePath.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
					string newPath = Path.Combine(targetDir, relativePath);

					// Ensure the target subdirectory exists
					string newFileDir = Path.GetDirectoryName(newPath);
					if (!Directory.Exists(newFileDir))
					{
						Directory.CreateDirectory(newFileDir);
						MoveResults.Add(new CRenameResultViewModel
						{
							Description = $"Created directory: {newFileDir}",
							IsSuccess = true
						});
					}

					moveMap[file.FilePath] = newPath;
				}

				// Step 1: Update CMakeLists.txt files
				ProgressMessage = "Updating CMakeLists.txt files...";
				await FileOperationHelper.UpdateCMakeFilesAsync(moveMap, MoveResults);

				// Step 2: If user selected a new CMake file + uber + group, add entries there
				if (CMakeSelection.HasValidSelection)
				{
					ProgressMessage = "Adding files to new CMake location...";
					await CMakeSelection.AddFilesToCMakeLocationAsync(moveMap, MoveResults);
				}

				// Step 3: Update #include references
				ProgressMessage = "Searching for #include references...";
				await FileOperationHelper.UpdateIncludeReferencesAsync(
					moveMap, sourceDir, MoveResults, msg => ProgressMessage = msg);

				// Step 3: Move the actual files on disk
				ProgressMessage = "Moving files on disk...";
				await FileOperationHelper.MoveFilesOnDiskAsync(moveMap, MoveResults);

				MoveResults.Add(new CRenameResultViewModel
				{
					Description = $"Move completed successfully! Moved {moveMap.Count} file(s).",
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
				errorMessageBox.ShowError($"[MoveFolder] Exception during folder move operation: {ex.Message}!");
			}
			finally
			{
				IsMoving = false;
				ProgressMessage = string.Empty;
			}
		}

		[RelayCommand]
		private void Close()
		{
			OnRequestClose?.Invoke(this, EventArgs.Empty);
		}

		// Properties
		private string m_windowTitle = "Move Folder";
		public string WindowTitle { get => m_windowTitle; set => SetProperty(ref m_windowTitle, value); }

		private string m_sourceDirectory = string.Empty;
		public string SourceDirectory { get => m_sourceDirectory; set => SetProperty(ref m_sourceDirectory, value); }

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

		private ObservableCollection<CRelatedFileViewModel> m_discoveredFiles = new ObservableCollection<CRelatedFileViewModel>();
		public ObservableCollection<CRelatedFileViewModel> DiscoveredFiles { get => m_discoveredFiles; set => SetProperty(ref m_discoveredFiles, value); }

		private ObservableCollection<CRenameResultViewModel> m_moveResults = new ObservableCollection<CRenameResultViewModel>();
		public ObservableCollection<CRenameResultViewModel> MoveResults { get => m_moveResults; set => SetProperty(ref m_moveResults, value); }

		// CMake selection helper (shared across all commands)
		public CCMakeSelectionHelper CMakeSelection { get; } = new CCMakeSelectionHelper();

		public event EventHandler OnRequestClose;
	}
}
