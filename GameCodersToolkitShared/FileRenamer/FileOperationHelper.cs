using GameCodersToolkit.FileRenamer.ViewModels;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.SourceControl;
using GameCodersToolkit.Utils;
using GameCodersToolkit.Configuration;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace GameCodersToolkit.FileRenamer
{
	/// <summary>
	/// Shared helper methods for file move/rename operations.
	/// Used by RenameFileDialogViewModel, MoveFilesDialogViewModel, and MoveFolderDialogViewModel.
	/// </summary>
	public static class FileOperationHelper
	{
		/// <summary>
		/// Source file extensions used across all file tools (e.g. related-file discovery, include scanning).
		/// </summary>
		public static readonly string[] SourceExtensions = new[] { ".h", ".cpp", ".inl", ".hpp", ".cxx", ".c" };

		/// <summary>
		/// Glob patterns for <see cref="Directory.GetFiles"/> when scanning for source files.
		/// </summary>
		public static readonly string[] SourceGlobPatterns = new[] { "*.h", "*.cpp", "*.inl", "*.hpp", "*.cxx", "*.c" };

		/// <summary>
		/// Open-file-dialog filter string for source files.
		/// </summary>
		public const string SourceFileDialogFilter = "Source Files (*.h;*.cpp;*.inl;*.hpp;*.cxx;*.c)|*.h;*.cpp;*.inl;*.hpp;*.cxx;*.c|All Files (*.*)|*.*";

		public static string FindOwningCMakeRoot(string directory)
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

		public static string FindProjectRoot(string startDirectory)
		{
			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			if (config?.CreatorConfig?.CMakeFileEntries != null)
			{
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

			return startDirectory;
		}

		public static async Task<bool> CheckPerforceAndConfirmAsync(IEnumerable<string> filePaths)
		{
			List<string> warnings = new List<string>();

			if (!PerforceConnection.IsEnabled)
			{
				warnings.Add("Perforce integration is disabled. Files will only be moved on disk.");
			}
			else if (!PerforceConnection.IsConnected)
			{
				warnings.Add("There is no active Perforce connection. Files will only be moved on disk and Perforce state will be out of sync.");
			}
			else
			{
				foreach (string filePath in filePaths)
				{
					bool checkoutOk = await PerforceConnection.TryCheckoutFilesAsync(new string[] { filePath });
					if (!checkoutOk)
					{
						warnings.Add($"Failed to check out '{Path.GetFileName(filePath)}' from Perforce.");
					}
				}
			}

			if (warnings.Count == 0)
				return true;

			string warningMessage = string.Join("\n", warnings)
				+ "\n\nDo you want to continue anyway?";

			var result = System.Windows.MessageBox.Show(
				warningMessage,
				"Perforce Warning",
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning);

			return result == MessageBoxResult.Yes;
		}

		public static async Task UpdateCMakeFilesAsync(Dictionary<string, string> renameMap, ObservableCollection<CRenameResultViewModel> results)
		{
			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			if (config?.CreatorConfig?.CMakeFileEntries == null)
			{
				results.Add(new CRenameResultViewModel
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

					string oldRelativeForward = oldRelative.Replace('\\', '/');
					string newRelativeForward = newRelative.Replace('\\', '/');

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
					await PerforceConnection.TryCheckoutFilesAsync(new string[] { cmakePath });

					if (!cmakePath.IsFileWritable())
					{
						cmakePath.MakeFileWritable();
					}

					if (cmakePath.IsFileWritable())
					{
						File.WriteAllText(cmakePath, modifiedContent);
						results.Add(new CRenameResultViewModel
						{
							Description = $"Updated CMake file: {Path.GetFileName(cmakePath)}",
							IsSuccess = true
						});
					}
					else
					{
						results.Add(new CRenameResultViewModel
						{
							Description = $"Failed to write to CMake file (not writable): {Path.GetFileName(cmakePath)}",
							IsSuccess = false
						});
					}
				}
			}
		}

		public static async Task UpdateIncludeReferencesAsync(
			Dictionary<string, string> renameMap,
			string searchRootDirectory,
			ObservableCollection<CRenameResultViewModel> results,
			Action<string> setProgressMessage)
		{
			string searchRoot = FindProjectRoot(searchRootDirectory);

			if (string.IsNullOrEmpty(searchRoot))
			{
				results.Add(new CRenameResultViewModel
				{
					Description = "Could not determine project root - skipping include reference updates.",
					IsSuccess = true
				});
				return;
			}

			var includeReplacements = new List<(Regex Pattern, string OldFilePath, string NewFilePath)>();
			foreach (var pair in renameMap)
			{
				string oldFileName = Path.GetFileName(pair.Key);
				string escapedOldName = Regex.Escape(oldFileName);

				Regex pattern = new Regex(
					$@"(#\s*include\s*[""<])([^"">\r\n]*[/\\])?({escapedOldName})(\s*["">])",
					RegexOptions.Compiled);

				includeReplacements.Add((pattern, pair.Key, pair.Value));
			}

			var scanResults = await Task.Run(() =>
			{
				List<string> sourceFiles = new List<string>();

				foreach (string ext in SourceGlobPatterns)
				{
					sourceFiles.AddRange(Directory.GetFiles(searchRoot, ext, SearchOption.AllDirectories));
				}

				setProgressMessage?.Invoke($"Scanning 0 / {sourceFiles.Count} files for #include references...");

				var changedFiles = new List<(string SourceFile, string ModifiedContent)>();
				var changeLog = new List<(string FilePath, int LineNumber, string OldInclude, string NewInclude)>();

				for (int i = 0; i < sourceFiles.Count; i++)
				{
					if (i % 50 == 0)
					{
						setProgressMessage?.Invoke($"Scanning {i} / {sourceFiles.Count} files for #include references...");
					}

					string sourceFile = sourceFiles[i];
					string content = File.ReadAllText(sourceFile);
					string modifiedContent = content;
					bool hasChanges = false;

					string effectiveSourcePath = sourceFile;
					foreach (var pair in renameMap)
					{
						if (string.Equals(Path.GetFullPath(pair.Key), Path.GetFullPath(sourceFile), StringComparison.OrdinalIgnoreCase))
						{
							effectiveSourcePath = pair.Value;
							break;
						}
					}

					foreach (var (pattern, oldFilePath, newFilePath) in includeReplacements)
					{
						if (pattern.IsMatch(modifiedContent))
						{
							string newFileName = Path.GetFileName(newFilePath);

							string oldDir = Path.GetFullPath(Path.GetDirectoryName(oldFilePath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
							string newDir = Path.GetFullPath(Path.GetDirectoryName(newFilePath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
							bool directoryChanged = !string.Equals(oldDir, newDir, StringComparison.OrdinalIgnoreCase);

							foreach (Match match in pattern.Matches(modifiedContent))
							{
								int lineNumber = modifiedContent.Substring(0, match.Index).Count(c => c == '\n') + 1;
								string oldIncludeText = match.Value.Trim();
								string newIncludeText;

								if (directoryChanged)
								{
									string newRelativePath = effectiveSourcePath.MakeRelativePath(newFilePath).Replace('\\', '/');
									string prefix = match.Groups[1].Value;
									string suffix = match.Groups[4].Value;
									newIncludeText = $"{prefix}{newRelativePath}{suffix}";
								}
								else
								{
									string prefix = match.Groups[1].Value;
									string pathPart = match.Groups[2].Value;
									string suffix = match.Groups[4].Value;
									newIncludeText = $"{prefix}{pathPart}{newFileName}{suffix}";
								}

								changeLog.Add((sourceFile, lineNumber, oldIncludeText, newIncludeText.Trim()));
							}

							if (directoryChanged)
							{
								modifiedContent = pattern.Replace(modifiedContent, match =>
								{
									string prefix = match.Groups[1].Value;
									string suffix = match.Groups[4].Value;
									string newRelativePath = effectiveSourcePath.MakeRelativePath(newFilePath);
									newRelativePath = newRelativePath.Replace('\\', '/');
									return $"{prefix}{newRelativePath}{suffix}";
								});
							}
							else
							{
								modifiedContent = pattern.Replace(modifiedContent, $"${{1}}${{2}}{newFileName}${{4}}");
							}
							hasChanges = true;
						}
					}

					if (hasChanges)
					{
						changedFiles.Add((sourceFile, modifiedContent));
					}
				}

				setProgressMessage?.Invoke($"Scanned {sourceFiles.Count} files. Applying changes to {changedFiles.Count} file(s)...");
				return (changedFiles, changeLog);
			});

			int updatedFileCount = 0;
			for (int i = 0; i < scanResults.changedFiles.Count; i++)
			{
				var (sourceFile, modifiedContent) = scanResults.changedFiles[i];
				setProgressMessage?.Invoke($"Writing changes to file {i + 1} / {scanResults.changedFiles.Count}...");

				await PerforceConnection.TryCheckoutFilesAsync(new string[] { sourceFile });

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
					results.Add(new CRenameResultViewModel
					{
						Description = $"Failed to update includes in (not writable): {Path.GetFileName(sourceFile)}",
						IsSuccess = false
					});
				}
			}

			if (scanResults.changeLog.Count > 0)
			{
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync("[FileTools] === #include reference changes ===");
				foreach (var (filePath, lineNumber, oldInclude, newInclude) in scanResults.changeLog)
				{
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync(
						$"{filePath}({lineNumber}): Changed '{oldInclude}' -> '{newInclude}'");
				}
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTools] === Total: {scanResults.changeLog.Count} include(s) changed in {updatedFileCount} file(s) ===");
			}

			if (updatedFileCount > 0)
			{
				results.Add(new CRenameResultViewModel
				{
					Description = $"Updated #include references in {updatedFileCount} file(s).",
					IsSuccess = true
				});
			}
			else
			{
				results.Add(new CRenameResultViewModel
				{
					Description = "No #include references found that needed updating.",
					IsSuccess = true
				});
			}
		}

		public static async Task MoveFileOnDiskAsync(string oldPath, string newPath, ObservableCollection<CRenameResultViewModel> results)
		{
			try
			{
				if (File.Exists(oldPath))
				{
					await PerforceConnection.TryCheckoutFilesAsync(new string[] { oldPath });
					bool p4MoveSucceeded = await PerforceConnection.TryMoveFilesAsync(oldPath, newPath);

					string moveDescription = $"{Path.GetFileName(oldPath)} -> {newPath}";

					if (p4MoveSucceeded)
					{
						await CloseDocumentIfOpenAsync(oldPath);

						results.Add(new CRenameResultViewModel
						{
							Description = $"Moved (via Perforce): {moveDescription}",
							IsSuccess = true
						});
					}
					else
					{
						if (!oldPath.IsFileWritable())
						{
							oldPath.MakeFileWritable();
						}

						if (oldPath.IsFileWritable())
						{
							await CloseDocumentIfOpenAsync(oldPath);
							File.Move(oldPath, newPath);

							results.Add(new CRenameResultViewModel
							{
								Description = $"Moved (on disk): {moveDescription}",
								IsSuccess = true
							});
						}
						else
						{
							results.Add(new CRenameResultViewModel
							{
								Description = $"Failed to move (not writable): {Path.GetFileName(oldPath)}",
								IsSuccess = false
							});
						}
					}
				}
			}
			catch (Exception ex)
			{
				results.Add(new CRenameResultViewModel
				{
					Description = $"Error moving {Path.GetFileName(oldPath)}: {ex.Message}",
					IsSuccess = false
				});
			}
		}

		public static async Task MoveFilesOnDiskAsync(Dictionary<string, string> moveMap, ObservableCollection<CRenameResultViewModel> results)
		{
			foreach (var pair in moveMap)
			{
				await MoveFileOnDiskAsync(pair.Key, pair.Value, results);
			}
		}

		private static async Task CloseDocumentIfOpenAsync(string filePath)
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
				// Best effort
			}
		}
	}
}
