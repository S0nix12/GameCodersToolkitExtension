using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.SourceControl;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using GameCodersToolkit.Utils;
using System.Threading.Tasks;

namespace GameCodersToolkit.FileRenamer.ViewModels
{
	public partial class CRelatedFileViewModel : ObservableObject
	{
		public string FilePath { get; set; }
		public string FileName => Path.GetFileName(FilePath);
		public string Extension => Path.GetExtension(FilePath);

		private bool m_isSelected = true;
		public bool IsSelected { get => m_isSelected; set => SetProperty(ref m_isSelected, value); }

		private bool m_exists = true;
		public bool Exists { get => m_exists; set => SetProperty(ref m_exists, value); }
	}

	public partial class CRenameResultViewModel : ObservableObject
	{
		public string Description { get; set; }
		public bool IsSuccess { get; set; }
	}

	public partial class CRenameFileDialogViewModel : ObservableObject
	{
		private readonly string m_activeFilePath;
		private readonly string m_originalBaseName;
		private readonly string m_originalDirectory;
		private readonly string m_owningCMakeRoot; // directory of the owning CMakeFile

		public CRenameFileDialogViewModel(string activeFilePath)
		{
			m_activeFilePath = activeFilePath;
			m_originalBaseName = Path.GetFileNameWithoutExtension(activeFilePath);
			m_originalDirectory = Path.GetDirectoryName(activeFilePath);

			NewFileName = m_originalBaseName;
			NewDirectory = m_originalDirectory;
			WindowTitle = $"Rename / Move File: {m_originalBaseName}";

			// Determine the owning CMake file for folder constraint validation
			m_owningCMakeRoot = FileOperationHelper.FindOwningCMakeRoot(m_originalDirectory);
			OwningCMakeFileDisplay = m_owningCMakeRoot ?? "(none found)";

			CMakeSelection.InitializeCMakeFileList();
			DiscoverRelatedFiles();
		}

		private void DiscoverRelatedFiles()
		{
			RelatedFiles.Clear();

			// Find all files in the same directory with the same base name but different extensions
			if (Directory.Exists(m_originalDirectory))
			{
				foreach (string extension in FileOperationHelper.SourceExtensions)
				{
					string candidatePath = Path.Combine(m_originalDirectory, m_originalBaseName + extension);
					if (File.Exists(candidatePath))
					{
						CRelatedFileViewModel fileVm = new CRelatedFileViewModel
						{
							FilePath = candidatePath,
							Exists = true
						};
						RelatedFiles.Add(fileVm);
					}
				}

				// If the active file wasn't found via the extension list (unusual extension), add it
				if (!RelatedFiles.Any(f => string.Equals(f.FilePath, m_activeFilePath, StringComparison.OrdinalIgnoreCase)))
				{
					RelatedFiles.Insert(0, new CRelatedFileViewModel
					{
						FilePath = m_activeFilePath,
						Exists = true
					});
				}
			}
		}

		private bool ValidateNewName()
		{
			ErrorMessage = string.Empty;

			if (string.IsNullOrWhiteSpace(NewFileName))
			{
				ErrorMessage = "File name cannot be empty.";
				return false;
			}

			if (!NewFileName.IsValidFileName())
			{
				ErrorMessage = "File name contains invalid characters.";
				return false;
			}

			bool nameChanged = !string.Equals(NewFileName, m_originalBaseName, StringComparison.Ordinal);
			string normalizedNewDir = Path.GetFullPath(NewDirectory ?? m_originalDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string normalizedOldDir = Path.GetFullPath(m_originalDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			bool dirChanged = !string.Equals(normalizedNewDir, normalizedOldDir, StringComparison.OrdinalIgnoreCase);

			if (!nameChanged && !dirChanged)
			{
				ErrorMessage = "Nothing to do: name and location are unchanged.";
				return false;
			}

			// Validate that NewDirectory is same or subfolder of the owning CMake root
			if (dirChanged && m_owningCMakeRoot != null)
			{
				string normalizedRoot = Path.GetFullPath(m_owningCMakeRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				if (!normalizedNewDir.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
				{
					ErrorMessage = "New location must be in the same folder or a subfolder of the owning CMake file's directory.";
					return false;
				}
			}

			if (!Directory.Exists(normalizedNewDir))
			{
				var result = System.Windows.MessageBox.Show(
					$"Directory does not exist:\n{normalizedNewDir}\n\nDo you want to create it?",
					"Create Directory?",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);

				if (result != MessageBoxResult.Yes)
					return false;

				try
				{
					Directory.CreateDirectory(normalizedNewDir);
				}
				catch (Exception ex)
				{
					ErrorMessage = $"Failed to create directory: {ex.Message}";
					return false;
				}
			}

			// Check if any of the new file paths already exist
			List<string> conflicts = new List<string>();
			foreach (var file in RelatedFiles.Where(f => f.IsSelected))
			{
				string newPath = Path.Combine(normalizedNewDir, NewFileName + file.Extension);
				if (File.Exists(newPath))
				{
					conflicts.Add(Path.GetFileName(newPath));
				}
			}

			if (conflicts.Count > 0)
			{
				ErrorMessage = $"File(s) already exist: {string.Join(", ", conflicts)}";
				return false;
			}

			return true;
		}

		[RelayCommand]
		private void BrowseForFolder()
		{
			string selectedPath = ModernFolderPicker.ShowDialog(NewDirectory, "Select new location for the file(s)");
			if (!string.IsNullOrEmpty(selectedPath))
			{
				NewDirectory = selectedPath;
			}
		}

		[RelayCommand]
		private async Task RenameAsync()
		{
			if (!ValidateNewName())
				return;

			List<CRelatedFileViewModel> filesToRename = RelatedFiles.Where(f => f.IsSelected).ToList();
			if (filesToRename.Count == 0)
			{
				ErrorMessage = "No files selected for renaming.";
				return;
			}

			// Pre-check: warn if Perforce is not available
			if (!await FileOperationHelper.CheckPerforceAndConfirmAsync(filesToRename.Select(f => f.FilePath)))
				return;

			IsRenaming = true;
			RenameResults.Clear();
			ProgressMessage = "Preparing...";

			try
			{
				// Use the chosen target directory (may be different from original if moving)
				string targetDirectory = Path.GetFullPath(NewDirectory ?? m_originalDirectory);

				// Build the mapping of old path -> new path
				Dictionary<string, string> renameMap = new Dictionary<string, string>();
				foreach (var file in filesToRename)
				{
					string newPath = Path.Combine(targetDirectory, NewFileName + file.Extension);
					renameMap[file.FilePath] = newPath;
				}

				if (CMakeSelection.HasValidSelection)
				{
					// When moving to a new CMake location: remove from old first, then add to new
					ProgressMessage = "Removing files from old CMake location...";
					await FileOperationHelper.RemoveFilesFromOldCMakeLocationsAsync(renameMap.Keys, RenameResults);

					ProgressMessage = "Adding files to new CMake location...";
					await CMakeSelection.AddFilesToCMakeLocationAsync(renameMap, RenameResults);
				}
				else
				{
					// No new CMake location selected: update paths in-place via string replacement
					ProgressMessage = "Updating CMakeLists.txt files...";
					await FileOperationHelper.UpdateCMakeFilesAsync(renameMap, RenameResults);
				}

				// Step 3: Update #include references in the project
				ProgressMessage = "Searching for #include references...";
				await FileOperationHelper.UpdateIncludeReferencesAsync(
					renameMap, m_originalDirectory, RenameResults, msg => ProgressMessage = msg);

				// Step 3: Rename the actual files on disk
				ProgressMessage = "Renaming/moving files on disk...";
				await RenameFilesOnDiskAsync(renameMap);

				RenameResults.Add(new CRenameResultViewModel
				{
					Description = "Rename / move completed successfully!",
					IsSuccess = true
				});

				HasCompleted = true;
			}
			catch (Exception ex)
			{
				RenameResults.Add(new CRenameResultViewModel
				{
					Description = $"Error during rename: {ex.Message}",
					IsSuccess = false
				});

				Community.VisualStudio.Toolkit.MessageBox errorMessageBox = new Community.VisualStudio.Toolkit.MessageBox();
				errorMessageBox.ShowError($"[FileRenamer] Exception during file rename operation: {ex.Message}!");
			}
			finally
			{
				IsRenaming = false;
				ProgressMessage = string.Empty;
			}
		}

		private async Task RenameFilesOnDiskAsync(Dictionary<string, string> renameMap)
		{
			foreach (var pair in renameMap)
			{
				string oldPath = pair.Key;
				string newPath = pair.Value;

				try
				{
					if (File.Exists(oldPath))
					{
						// First try to rename via Perforce (p4 move)
						// This requires the file to be checked out first
						await PerforceConnection.TryCheckoutFilesAsync(new string[] { oldPath });
						bool p4MoveSucceeded = await PerforceConnection.TryMoveFilesAsync(oldPath, newPath);

						string moveDescription = !string.Equals(Path.GetDirectoryName(oldPath), Path.GetDirectoryName(newPath), StringComparison.OrdinalIgnoreCase)
							? $"{Path.GetFileName(oldPath)} -> {newPath}"
							: $"{Path.GetFileName(oldPath)} -> {Path.GetFileName(newPath)}";

						if (p4MoveSucceeded)
						{
							// P4 move succeeded - the file has been moved on disk and in Perforce
							await FileOperationHelper.CloseDocumentIfOpenAsync(oldPath);

							RenameResults.Add(new CRenameResultViewModel
							{
								Description = $"Moved (via Perforce): {moveDescription}",
								IsSuccess = true
							});

							await VS.Documents.OpenAsync(newPath);
						}
						else
						{
							// P4 move failed or P4 is not available - fall back to filesystem rename
							if (!oldPath.IsFileWritable())
							{
								oldPath.MakeFileWritable();
							}

							if (oldPath.IsFileWritable())
							{
								await FileOperationHelper.CloseDocumentIfOpenAsync(oldPath);
								File.Move(oldPath, newPath);

								RenameResults.Add(new CRenameResultViewModel
								{
									Description = $"Moved (on disk): {moveDescription}",
									IsSuccess = true
								});

								await VS.Documents.OpenAsync(newPath);
							}
							else
							{
								RenameResults.Add(new CRenameResultViewModel
								{
									Description = $"Failed to rename/move (not writable): {Path.GetFileName(oldPath)}",
									IsSuccess = false
								});
							}
						}
					}
				}
				catch (Exception ex)
				{
					RenameResults.Add(new CRenameResultViewModel
					{
						Description = $"Error renaming {Path.GetFileName(oldPath)}: {ex.Message}",
						IsSuccess = false
					});
				}
			}
		}

		[RelayCommand]
		private void Close()
		{
			OnRequestClose?.Invoke(this, EventArgs.Empty);
		}

		// Properties
		private string m_windowTitle = "Rename File";
		public string WindowTitle { get => m_windowTitle; set => SetProperty(ref m_windowTitle, value); }

		private string m_newFileName = string.Empty;
		public string NewFileName { get => m_newFileName; set => SetProperty(ref m_newFileName, value); }

		private string m_errorMessage = string.Empty;
		public string ErrorMessage { get => m_errorMessage; set => SetProperty(ref m_errorMessage, value); }

		private bool m_isRenaming = false;
		public bool IsRenaming { get => m_isRenaming; set => SetProperty(ref m_isRenaming, value); }

		private bool m_hasCompleted = false;
		public bool HasCompleted { get => m_hasCompleted; set => SetProperty(ref m_hasCompleted, value); }

		private string m_newDirectory = string.Empty;
		public string NewDirectory { get => m_newDirectory; set => SetProperty(ref m_newDirectory, value); }

		private string m_owningCMakeFileDisplay = string.Empty;
		public string OwningCMakeFileDisplay { get => m_owningCMakeFileDisplay; set => SetProperty(ref m_owningCMakeFileDisplay, value); }

		private string m_progressMessage = string.Empty;
		public string ProgressMessage { get => m_progressMessage; set => SetProperty(ref m_progressMessage, value); }

		private ObservableCollection<CRelatedFileViewModel> m_relatedFiles = new ObservableCollection<CRelatedFileViewModel>();
		public ObservableCollection<CRelatedFileViewModel> RelatedFiles { get => m_relatedFiles; set => SetProperty(ref m_relatedFiles, value); }

		private ObservableCollection<CRenameResultViewModel> m_renameResults = new ObservableCollection<CRenameResultViewModel>();
		public ObservableCollection<CRenameResultViewModel> RenameResults { get => m_renameResults; set => SetProperty(ref m_renameResults, value); }

		// CMake selection helper (shared across all commands)
		public CCMakeSelectionHelper CMakeSelection { get; } = new CCMakeSelectionHelper();

		public event EventHandler OnRequestClose;
	}
}
