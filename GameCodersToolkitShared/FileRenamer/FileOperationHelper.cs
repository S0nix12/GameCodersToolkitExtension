using GameCodersToolkit.FileRenamer.ViewModels;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using GameCodersToolkit.SourceControl;
using GameCodersToolkit.Utils;
using GameCodersToolkit.Configuration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
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

		/// <summary>
		/// Computes the new #include path for a moved file.
		/// For co-located same-name files (e.g. MyFile.cpp including MyFile.h in the same directory),
		/// returns just the filename. For all other files, returns a path relative to the CMake root directory.
		/// </summary>
		public static string ComputeNewIncludePath(string cmakeRoot, string effectiveSourcePath, string newFilePath)
		{
			string sourceDir = Path.GetFullPath(Path.GetDirectoryName(effectiveSourcePath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			string targetDir = Path.GetFullPath(Path.GetDirectoryName(newFilePath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

			// Co-located same-name files use simple filename includes (e.g. #include "MyFile.h")
			if (string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase) &&
				string.Equals(Path.GetFileNameWithoutExtension(effectiveSourcePath), Path.GetFileNameWithoutExtension(newFilePath), StringComparison.OrdinalIgnoreCase))
			{
				return Path.GetFileName(newFilePath);
			}

			// All other includes are relative to the CMakeLists.txt directory
			string rootDir = Path.GetFullPath(cmakeRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
			Uri rootUri = new Uri(rootDir);
			Uri targetUri = new Uri(Path.GetFullPath(newFilePath));
			return Uri.UnescapeDataString(rootUri.MakeRelativeUri(targetUri).ToString()).Replace('\\', '/');
		}

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

				// Run file read + string processing on a background thread
				string modifiedContent = await Task.Run(() =>
				{
					string cmakeContent = File.ReadAllText(cmakePath);
					string modified = cmakeContent;
					bool hasChanges = false;

					foreach (var pair in renameMap)
					{
						string oldRelative = cmakePath.MakeRelativePath(pair.Key);
						string newRelative = cmakePath.MakeRelativePath(pair.Value);

						string oldRelativeForward = oldRelative.Replace('\\', '/');
						string newRelativeForward = newRelative.Replace('\\', '/');

						string oldFileName = Path.GetFileName(pair.Key);
						string newFileName = Path.GetFileName(pair.Value);

						if (modified.Contains(oldRelativeForward))
						{
							modified = modified.Replace(oldRelativeForward, newRelativeForward);
							hasChanges = true;
						}
						else if (modified.Contains(oldRelative))
						{
							modified = modified.Replace(oldRelative, newRelative);
							hasChanges = true;
						}
						else if (modified.Contains(oldFileName))
						{
							modified = modified.Replace(oldFileName, newFileName);
							hasChanges = true;
						}
					}

					return hasChanges ? modified : null;
				});

				if (modifiedContent != null)
				{
					await PerforceConnection.TryCheckoutFilesAsync(new string[] { cmakePath });

					if (!cmakePath.IsFileWritable())
					{
						cmakePath.MakeFileWritable();
					}

					if (cmakePath.IsFileWritable())
					{
						await Task.Run(() => File.WriteAllText(cmakePath, modifiedContent));
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

				var changedFiles = new ConcurrentBag<(string SourceFile, string ModifiedContent)>();
				var changeLog = new ConcurrentBag<(string FilePath, int LineNumber, string OldInclude, string NewInclude)>();
				int filesProcessed = 0;

				Parallel.ForEach(sourceFiles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, sourceFile =>
				{
					int current = Interlocked.Increment(ref filesProcessed);
					if (current % 200 == 0)
					{
						setProgressMessage?.Invoke($"Scanning {current} / {sourceFiles.Count} files for #include references...");
					}

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
									string newRelativePath = ComputeNewIncludePath(searchRoot, effectiveSourcePath, newFilePath);
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
								string newRelativePath = ComputeNewIncludePath(searchRoot, effectiveSourcePath, newFilePath);
								modifiedContent = pattern.Replace(modifiedContent, match =>
								{
									string prefix = match.Groups[1].Value;
									string suffix = match.Groups[4].Value;
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
				});

				setProgressMessage?.Invoke($"Scanned {sourceFiles.Count} files. Applying changes to {changedFiles.Count} file(s)...");
				return (changedFiles: changedFiles.ToList(), changeLog: changeLog.ToList());
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
					await Task.Run(() => File.WriteAllText(sourceFile, modifiedContent));
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

		/// <summary>
		/// Scans all configured CMake files for the old file paths (from the move map keys),
		/// removes matching file entries, and cleans up any groups or uber files that become empty.
		/// This should be called BEFORE UpdateCMakeFilesAsync (which does path string replacement)
		/// to ensure a clean removal from the source CMake file when moving to a different CMake location.
		/// </summary>
		public static async Task RemoveFilesFromOldCMakeLocationsAsync(
			IEnumerable<string> oldFilePaths,
			ObservableCollection<CRenameResultViewModel> results)
		{
			CFileTemplateCreatorConfiguration config = GameCodersToolkitPackage.FileTemplateCreatorConfig;
			if (config?.CreatorConfig?.CMakeFileEntries == null)
				return;

			IMakeFileParser parser = config.CreateParser();
			if (parser == null)
				return;

			// Normalize old file paths for comparison
			HashSet<string> normalizedOldPaths = new HashSet<string>(
				oldFilePaths.Select(p => Path.GetFullPath(p).Replace('\\', '/').ToLowerInvariant()));

			foreach (CMakeFileEntry cmakeEntry in config.CreatorConfig.CMakeFileEntries)
			{
				string cmakePath = cmakeEntry.AbsolutePath;
				if (string.IsNullOrEmpty(cmakePath) || !File.Exists(cmakePath))
					continue;

				// Run the heavy parse + removal work on a background thread
				var removalResult = await Task.Run(() =>
				{
					IMakeFile makeFile = parser.Parse(cmakePath);
					if (makeFile == null)
						return (MakeFile: (IMakeFile)null, MadeChanges: false, RemovedEntries: (List<string>)null, StepResults: (List<CRenameResultViewModel>)null);

					string cmakeDir = Path.GetDirectoryName(cmakePath);
					bool madeChanges = false;
					List<string> removedEntries = new List<string>();
					List<CRenameResultViewModel> stepResults = new List<CRenameResultViewModel>();

					// Re-scan from scratch after each structural change,
					// since removals invalidate node references.
					bool foundInPass;
					do
					{
						foundInPass = false;

						foreach (IUberFileNode uberFile in makeFile.GetUberFiles().ToList())
						{
							if (foundInPass) break;

							foreach (IGroupNode group in uberFile.GetGroups().ToList())
							{
								List<IFileNode> filesToRemove = new List<IFileNode>();
								foreach (IFileNode fileNode in group.GetFiles())
								{
									string resolvedPath = Path.GetFullPath(Path.Combine(cmakeDir, fileNode.GetName().Replace('/', '\\')))
										.Replace('\\', '/').ToLowerInvariant();

									if (normalizedOldPaths.Contains(resolvedPath))
									{
										filesToRemove.Add(fileNode);
									}
								}

								if (filesToRemove.Count > 0)
								{
									makeFile = makeFile.RemoveFiles(uberFile, group, filesToRemove);
									madeChanges = true;
									foundInPass = true;

									foreach (var f in filesToRemove)
									{
										removedEntries.Add(f.GetName());
									}

									IUberFileNode updatedUber = makeFile.GetUberFiles()
										.FirstOrDefault(u => u.GetName() == uberFile.GetName());

									if (updatedUber != null)
									{
										IGroupNode updatedGroup = updatedUber.GetGroups()
											.FirstOrDefault(g => g.GetName() == group.GetName());

										if (updatedGroup != null && !updatedGroup.GetFiles().Any())
										{
											makeFile = makeFile.RemoveGroup(updatedUber, updatedGroup);

											stepResults.Add(new CRenameResultViewModel
											{
												Description = $"Removed empty group '{group.GetName()}' from uber file '{uberFile.GetName()}'",
												IsSuccess = true
											});
										}

										updatedUber = makeFile.GetUberFiles()
											.FirstOrDefault(u => u.GetName() == uberFile.GetName());

										if (updatedUber != null && !updatedUber.GetGroups().Any())
										{
											makeFile = makeFile.RemoveUberFile(updatedUber);

											stepResults.Add(new CRenameResultViewModel
											{
												Description = $"Removed empty uber file '{uberFile.GetName()}'",
												IsSuccess = true
											});
										}
									}

									break;
								}
							}
						}
					}
					while (foundInPass);

					return (MakeFile: makeFile, MadeChanges: madeChanges, RemovedEntries: removedEntries, StepResults: stepResults);
				});

				if (removalResult.MakeFile == null)
					continue;

				// Marshal intermediate results (empty group/uber file removals) to the UI collection
				foreach (var stepResult in removalResult.StepResults)
				{
					results.Add(stepResult);
				}

				if (removalResult.MadeChanges)
				{
					await PerforceConnection.TryCheckoutFilesAsync(new string[] { cmakePath });

					if (!cmakePath.IsFileWritable())
					{
						cmakePath.MakeFileWritable();
					}

					if (cmakePath.IsFileWritable())
					{
						await removalResult.MakeFile.SaveAsync();

						results.Add(new CRenameResultViewModel
						{
							Description = $"Removed {removalResult.RemovedEntries.Count} file entry(ies) from {Path.GetFileName(cmakePath)}",
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

		public static async Task CloseDocumentIfOpenAsync(string filePath)
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
