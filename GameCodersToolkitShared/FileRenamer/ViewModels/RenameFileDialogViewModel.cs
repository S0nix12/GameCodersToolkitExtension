using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameCodersToolkit.Configuration;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.SourceControl;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using GameCodersToolkit.Utils;

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
		private static readonly string[] RelatedExtensions = new[] { ".h", ".cpp", ".inl", ".hpp", ".cxx", ".c" };

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
			m_owningCMakeRoot = FindOwningCMakeRoot(m_originalDirectory);
			OwningCMakeFileDisplay = m_owningCMakeRoot ?? "(none found)";

			DiscoverRelatedFiles();
		}

		private void DiscoverRelatedFiles()
		{
			RelatedFiles.Clear();

			// Find all files in the same directory with the same base name but different extensions
			if (Directory.Exists(m_originalDirectory))
			{
				foreach (string extension in RelatedExtensions)
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

		private string FindOwningCMakeRoot(string directory)
		{
			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			if (config?.CreatorConfig?.CMakeFileEntries != null)
			{
				string bestMatch = null;
				int bestMatchLength = 0;

				foreach (CMakeFileEntry entry in config.CreatorConfig.CMakeFileEntries)
				{
					string cmakeDir = Path.GetDirectoryName(entry.AbsolutePath);
					if (directory.StartsWith(cmakeDir, StringComparison.OrdinalIgnoreCase) && cmakeDir.Length > bestMatchLength)
					{
						bestMatch = cmakeDir;
						bestMatchLength = cmakeDir.Length;
					}
				}

				if (bestMatch != null)
					return bestMatch;
			}

			// Walk up looking for CMakeLists.txt
			string current = directory;
			while (!string.IsNullOrEmpty(current))
			{
				if (File.Exists(Path.Combine(current, "CMakeLists.txt")))
					return current;

				string parent = Path.GetDirectoryName(current);
				if (parent == current)
					break;
				current = parent;
			}

			return directory;
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
				ErrorMessage = $"Directory does not exist: {normalizedNewDir}";
				return false;
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

				// Step 1: Find and update CMakeLists.txt files
				ProgressMessage = "Updating CMakeLists.txt files...";
				await UpdateCMakeFilesAsync(renameMap);

				// Step 2: Update #include references in the project
				ProgressMessage = "Searching for #include references...";
				await UpdateIncludeReferencesAsync(renameMap);

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

		private async Task UpdateCMakeFilesAsync(Dictionary<string, string> renameMap)
		{
			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			if (config?.CreatorConfig?.CMakeFileEntries == null)
			{
				RenameResults.Add(new CRenameResultViewModel
				{
					Description = "No CMakeLists configuration found - skipping CMake updates.",
					IsSuccess = true
				});
				return;
			}

			foreach (CMakeFileEntry cmakeEntry in config.CreatorConfig.CMakeFileEntries)
			{
				string cmakePath = cmakeEntry.AbsolutePath;
				if (!File.Exists(cmakePath))
					continue;

				string cmakeContent = File.ReadAllText(cmakePath);
				string modifiedContent = cmakeContent;
				bool hasChanges = false;

				foreach (var pair in renameMap)
				{
					string oldRelative = cmakePath.MakeRelativePath(pair.Key);
					string newRelative = cmakePath.MakeRelativePath(pair.Value);

					// Also try with backslashes replaced to forward slashes (common in CMake)
					string oldRelativeForward = oldRelative.Replace('\\', '/');
					string newRelativeForward = newRelative.Replace('\\', '/');

					// Try the old file name only (without directory relative path) as well
					string oldFileName = Path.GetFileName(pair.Key);
					string newFileName = Path.GetFileName(pair.Value);

					if (modifiedContent.Contains(oldRelativeForward))
					{
						modifiedContent = modifiedContent.Replace(oldRelativeForward, newRelativeForward);
						hasChanges = true;
					}
					else if (modifiedContent.Contains(oldRelative))
					{
						modifiedContent = modifiedContent.Replace(oldRelative, newRelative);
						hasChanges = true;
					}
					else if (modifiedContent.Contains(oldFileName))
					{
						modifiedContent = modifiedContent.Replace(oldFileName, newFileName);
						hasChanges = true;
					}
				}

				if (hasChanges)
				{
					// Try checking out via Perforce
					await PerforceConnection.TryCheckoutFilesAsync(new string[] { cmakePath });

					// Fallback: make writable
					if (!cmakePath.IsFileWritable())
					{
						cmakePath.MakeFileWritable();
					}

					if (cmakePath.IsFileWritable())
					{
						File.WriteAllText(cmakePath, modifiedContent);
						RenameResults.Add(new CRenameResultViewModel
						{
							Description = $"Updated CMake file: {Path.GetFileName(cmakePath)}",
							IsSuccess = true
						});
					}
					else
					{
						RenameResults.Add(new CRenameResultViewModel
						{
							Description = $"Failed to write to CMake file (not writable): {Path.GetFileName(cmakePath)}",
							IsSuccess = false
						});
					}
				}
			}
		}

		private async Task UpdateIncludeReferencesAsync(Dictionary<string, string> renameMap)
		{
			// Find the project directory: walk up from the file directory looking for a CMakeLists.txt
			string searchRoot = FindProjectRoot(m_originalDirectory);

			if (string.IsNullOrEmpty(searchRoot))
			{
				RenameResults.Add(new CRenameResultViewModel
				{
					Description = "Could not determine project root - skipping include reference updates.",
					IsSuccess = true
				});
				return;
			}

			// Build a list of (regex, old path, new path) for each old -> new mapping
			// When the file is being moved (directory changed), we need to recompute the full include path
			// relative to each including file, not just swap the filename.
			var includeReplacements = new List<(Regex Pattern, string OldFilePath, string NewFilePath)>();
			foreach (var pair in renameMap)
			{
				string oldFileName = Path.GetFileName(pair.Key);
				string escapedOldName = Regex.Escape(oldFileName);

				// Match #include "...oldFileName" or #include <...oldFileName>
				Regex pattern = new Regex(
					$@"(#\s*include\s*[""<])([^"">\r\n]*[/\\])?({escapedOldName})(\s*["">])",
					RegexOptions.Compiled);

				includeReplacements.Add((pattern, pair.Key, pair.Value));
			}

			// Search through all source files in the project
			string[] sourceExtensions = new[] { "*.cpp", "*.h", "*.inl", "*.hpp", "*.cxx", "*.c" };
			List<string> sourceFiles = new List<string>();

			foreach (string ext in sourceExtensions)
			{
				sourceFiles.AddRange(Directory.GetFiles(searchRoot, ext, SearchOption.AllDirectories));
			}

			int updatedFileCount = 0;

			foreach (string sourceFile in sourceFiles)
			{
				string content = File.ReadAllText(sourceFile);
				string modifiedContent = content;
				bool hasChanges = false;

				foreach (var (pattern, oldFilePath, newFilePath) in includeReplacements)
				{
					if (pattern.IsMatch(modifiedContent))
					{
						string newFileName = Path.GetFileName(newFilePath);

						// Check if the file was moved to a different directory
						string oldDir = Path.GetFullPath(Path.GetDirectoryName(oldFilePath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
						string newDir = Path.GetFullPath(Path.GetDirectoryName(newFilePath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
						bool directoryChanged = !string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase);

						if (directoryChanged)
						{
							// When directory changed, recompute the relative path from the including file to the new location
							modifiedContent = pattern.Replace(modifiedContent, match =>
							{
								string prefix = match.Groups[1].Value;  // #include " or #include <
								string suffix = match.Groups[4].Value;  // " or >

								// Compute new relative path from this source file to the new file location
								string newRelativePath = sourceFile.MakeRelativePath(newFilePath);
								// Normalize to forward slashes (standard for includes)
								newRelativePath = newRelativePath.Replace('\\', '/');

								return $"{prefix}{newRelativePath}{suffix}";
							});
						}
						else
						{
							// Same directory: just replace the filename, keep existing path prefix
							modifiedContent = pattern.Replace(modifiedContent, $"${{1}}${{2}}{newFileName}${{4}}");
						}
						hasChanges = true;
					}
				}

				if (hasChanges)
				{
					// Try checking out via Perforce
					await PerforceConnection.TryCheckoutFilesAsync(new string[] { sourceFile });

					// Fallback: make writable
					if (!sourceFile.IsFileWritable())
					{
						sourceFile.MakeFileWritable();
					}

					if (sourceFile.IsFileWritable())
					{
						File.WriteAllText(sourceFile, modifiedContent);
						updatedFileCount++;
					}
					else
					{
						RenameResults.Add(new CRenameResultViewModel
						{
							Description = $"Failed to update includes in (not writable): {Path.GetFileName(sourceFile)}",
							IsSuccess = false
						});
					}
				}
			}

			if (updatedFileCount > 0)
			{
				RenameResults.Add(new CRenameResultViewModel
				{
					Description = $"Updated #include references in {updatedFileCount} file(s).",
					IsSuccess = true
				});
			}
			else
			{
				RenameResults.Add(new CRenameResultViewModel
				{
					Description = "No #include references found that needed updating.",
					IsSuccess = true
				});
			}
		}

		private string FindProjectRoot(string startDirectory)
		{
			// Strategy: look for CMakeLists.txt going upwards, use the directory of the first one found
			// Also fall back to looking through the configured CMakeFileEntries
			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			if (config?.CreatorConfig?.CMakeFileEntries != null)
			{
				// Find the CMakeFileEntry whose file is "closest" parent of our file
				string bestMatch = null;
				int bestMatchLength = 0;

				foreach (CMakeFileEntry entry in config.CreatorConfig.CMakeFileEntries)
				{
					string cmakeDir = Path.GetDirectoryName(entry.AbsolutePath);
					if (startDirectory.StartsWith(cmakeDir, StringComparison.OrdinalIgnoreCase) && cmakeDir.Length > bestMatchLength)
					{
						bestMatch = cmakeDir;
						bestMatchLength = cmakeDir.Length;
					}
				}

				if (bestMatch != null)
					return bestMatch;
			}

			// Walk up looking for CMakeLists.txt
			string current = startDirectory;
			while (!string.IsNullOrEmpty(current))
			{
				if (File.Exists(Path.Combine(current, "CMakeLists.txt")))
					return current;

				string parent = Path.GetDirectoryName(current);
				if (parent == current)
					break;
				current = parent;
			}

			// Fallback to the file's own directory
			return startDirectory;
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
							await CloseDocumentIfOpenAsync(oldPath);

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
								await CloseDocumentIfOpenAsync(oldPath);
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

		private async Task CloseDocumentIfOpenAsync(string filePath)
		{
			try
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
				var dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE80.DTE2>();
				if (dte != null)
				{
					foreach (EnvDTE.Document doc in dte.Documents)
					{
						if (string.Equals(doc.FullName, filePath, StringComparison.OrdinalIgnoreCase))
						{
							doc.Close(EnvDTE.vsSaveChanges.vsSaveChangesPrompt);
							break;
						}
					}
				}
			}
			catch
			{
				// Best effort - if we can't close it, the rename might still work
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

		public event EventHandler OnRequestClose;
	}
}
