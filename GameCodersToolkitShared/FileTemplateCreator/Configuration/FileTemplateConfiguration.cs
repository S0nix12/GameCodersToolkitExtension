using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GameCodersToolkit.SourceControl;
using GameCodersToolkit.Utils;
using EnvDTE80;
using EnvDTE;

namespace GameCodersToolkit.Configuration
{
	public class CMakeFileEntry
	{
		public string ID { get; set; }
		public string Path { get; set; }

		[JsonIgnore]
		public string AbsolutePath { get; set; }
	}

	public class CTemplateEntry
	{
		public string Name { get; set; }
		public string MakeFileID { get; set; }
		public string Description { get; set; }
		public List<string> Paths { get; set; } = [];

		[JsonIgnore]
		public List<string> AbsolutePaths { get; set; } = [];
	}

	public class CFileTemplateCreatorConfig
	{
		public List<CMakeFileEntry> CMakeFileEntries { get; set; } = [];
		public List<CTemplateEntry> FileTemplateEntries { get; set; } = [];

		public string ParserName { get; set; }
		public string ParserConfigPath { get; set; }

		public string PostChangeScriptPath { get; set; }


		[Editable(allowEdit: true)]
		public string P4Server { get; set; }
		[Editable(allowEdit: true)]
		public string P4UserName { get; set; }
		[Editable(allowEdit: true)]
		public string P4Workspace { get; set; }
		[Editable(allowEdit: true)]
		public string AuthorName { get; set; }
		[Editable(allowEdit: true)]
		public string PostChangeProjectToBuild { get; set; }


		[JsonIgnore]
		public string PostChangeScriptAbsolutePath { get; set; }
		[JsonIgnore]
		public string ParserConfigPathAbsolute { get; set; }
		[JsonIgnore]
		public JsonDocument ParserConfig { get; set; }
	}

	public class CFileTemplateCreatorConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "FileTemplateCreator/FileTemplateCreatorConfig.json";

		public async Task InitAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			bool isSolutionOpen = await VS.Solutions.IsOpenAsync();

			if (isSolutionOpen)
			{
				HandleOpenSolution(await VS.Solutions.GetCurrentSolutionAsync());
			}

			VS.Events.SolutionEvents.OnAfterOpenSolution += HandleOpenSolution;
		}

		private void HandleOpenSolution(Community.VisualStudio.Toolkit.Solution solution = null)
		{
			if (solution != null)
			{
				lock (SolutionFolder)
				{
					SolutionFolder = Path.GetDirectoryName(solution.FullPath);
				}
			}
			ThreadHelper.JoinableTaskFactory.Run(LoadSolutionConfigAsync);
		}

		public async Task LoadSolutionConfigAsync()
		{
			string configFilePath = GetConfigFilePath();
			await LoadConfigAsync(configFilePath);

			ConfigFileWatcher?.Dispose();
			ConfigFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(configFilePath))
			{
				EnableRaisingEvents = true
			};
			ConfigFileWatcher.Changed += OnConfigFileChanged;
			ConfigFileWatcher.IncludeSubdirectories = false;
			ConfigFileWatcher.Filter = "*.json";
			ConfigFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastAccess;
		}

		private void OnConfigFileChanged(object sender, FileSystemEventArgs eventArgs)
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate { await LoadConfigAsync(eventArgs.FullPath); });
		}

		public string GetConfigFilePath()
		{
			lock (SolutionFolder)
			{
				string configFilePath = Path.Combine(SolutionFolder, cConfigFilePath);
				return configFilePath;
			}
		}

		public IMakeFileParser CreateParser()
		{
			Type parserType = Type.GetType(CreatorConfig.ParserName);
			if (parserType != null)
			{
				return Activator.CreateInstance(parserType) as IMakeFileParser;
			}

			return null;
		}

		public string FindMakeFilePathByID(string id)
		{
			return CreatorConfig.CMakeFileEntries.Where(makeFile => makeFile.ID == id).FirstOrDefault()?.AbsolutePath;
		}

		public CTemplateEntry FindTemplateByName(string name)
		{
			return CreatorConfig.FileTemplateEntries.Where(entry => entry.Name == name).FirstOrDefault();
		}

		public async Task<Type> GetParserConfigAsAsync<Type>()
		{
			Type result = default;
			try
			{
				result = JsonSerializer.Deserialize<Type>(CreatorConfig.ParserConfig);
			}
			catch (Exception ex)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync("[FileTemplateCreator] Exception while retrieving parser config", ex);
			}

			return result;
		}

		public Type GetParserConfigAs<Type>()
		{
			Type result = default;
			try
			{
				result = JsonSerializer.Deserialize<Type>(CreatorConfig.ParserConfig);
			}
			catch (Exception ex)
			{
				ThreadHelper.JoinableTaskFactory.Run(async delegate { await DiagnosticUtils.ReportExceptionFromExtensionAsync("[FileTemplateCreator] Exception while retrieving parser config", ex); });
			}

			return result;
		}

		public async Task ExecutePostBuildStepsAsync()
		{
			if (File.Exists(CreatorConfig.PostChangeScriptAbsolutePath))
			{
				System.Diagnostics.Process.Start(CreatorConfig.PostChangeScriptAbsolutePath);
            }

            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                DTE2 dte = GameCodersToolkitPackage.Package.GetService<EnvDTE.DTE, DTE2>();
                if (dte != null)
				{
                    bool isBuildingAlready = dte.Solution.SolutionBuild.BuildState == EnvDTE.vsBuildState.vsBuildStateInProgress;

					if (!isBuildingAlready && !string.IsNullOrWhiteSpace(CreatorConfig.PostChangeProjectToBuild))
					{
					    var project = await VS.Solutions.FindProjectsAsync(CreatorConfig.PostChangeProjectToBuild);
					    if (project != null)
					    {
					        project.BuildAsync(BuildAction.Build);
					    }
					}
				}
            });
		}

		private async Task LoadConfigAsync(string filePath)
		{
			try
			{
				if (CreatorConfig != null)
				{
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Reloading FileTemplateCreator config after change");
				}
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Attempting to load FileTemplateCreator config at '{filePath}'");

				if (File.Exists(filePath))
				{
					FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
					using var fileStream = new FileStream(
						filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

					CreatorConfig = await JsonSerializer.DeserializeAsync<CFileTemplateCreatorConfig>(fileStream);

					// Post-Change Script
					{
						if (!string.IsNullOrWhiteSpace(CreatorConfig.PostChangeScriptPath) && !Path.IsPathRooted(CreatorConfig.PostChangeScriptPath))
						{
							lock (SolutionFolder)
							{
								CreatorConfig.PostChangeScriptAbsolutePath = Path.Combine(SolutionFolder, CreatorConfig.PostChangeScriptPath);
							}

							CreatorConfig.PostChangeScriptAbsolutePath = Path.GetFullPath(CreatorConfig.PostChangeScriptAbsolutePath);
						}

						bool exists = File.Exists(CreatorConfig.PostChangeScriptAbsolutePath);
						await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] PostChangeScript at '{CreatorConfig.PostChangeScriptAbsolutePath}' (File exists: {exists})");
					}

					// Post-Change project to build
					{
						bool exists = (await VS.Solutions.FindProjectsAsync(CreatorConfig.PostChangeProjectToBuild)) != null;
						await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] PostChangeProjectToBuild is '{CreatorConfig.PostChangeProjectToBuild}' (Exists: {exists})");
					}

					// Parser config path
					{
						if (!string.IsNullOrWhiteSpace(CreatorConfig.ParserConfigPath))
						{
							lock (SolutionFolder)
							{
								CreatorConfig.ParserConfigPathAbsolute = Path.Combine(SolutionFolder, CreatorConfig.ParserConfigPath);
							}
						}

						bool exists = File.Exists(CreatorConfig.ParserConfigPathAbsolute);
						if (exists)
						{
							try
							{
								string fileContent = File.ReadAllText(CreatorConfig.ParserConfigPathAbsolute);
								CreatorConfig.ParserConfig = JsonDocument.Parse(fileContent);
							}
							catch (Exception ex)
							{
								await DiagnosticUtils.ReportExceptionFromExtensionAsync(
									"[FileTemplateCreator] Exception while retrieving parser configuration",
									ex);
							}
						}

						await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] ParserConfig at '{CreatorConfig.ParserConfigPathAbsolute}' (File exists: {exists})");
					}

					// Makefiles
					{
						List<CMakeFileEntry> entries = CreatorConfig.CMakeFileEntries;

						for (int i = entries.Count - 1; i >= 0; i--)
						{
							CMakeFileEntry cmakeFileEntry = entries[i];

							if (!Path.IsPathRooted(cmakeFileEntry.Path))
							{
								lock (SolutionFolder)
								{
									cmakeFileEntry.AbsolutePath = Path.Combine(SolutionFolder, cmakeFileEntry.Path);
								}
							}

							cmakeFileEntry.AbsolutePath = Path.GetFullPath(cmakeFileEntry.AbsolutePath);

							if (!File.Exists(cmakeFileEntry.AbsolutePath))
							{
								entries.RemoveAt(i);
								await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Couldn't find MakeFile at path '{cmakeFileEntry.AbsolutePath}'");
							}
						}

						await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Found {entries.Count} valid MakeFile entries");
					}

					// Templates
					{
						List<CTemplateEntry> entries = CreatorConfig.FileTemplateEntries;

						for (int i = entries.Count - 1; i >= 0; i--)
						{
							bool isValid = true;
							foreach (string path in entries[i].Paths)
							{
								string absolutePath = path;

								if (!Path.IsPathRooted(absolutePath))
								{
									lock (SolutionFolder)
									{
										absolutePath = Path.Combine(SolutionFolder, absolutePath);
									}
								}

								absolutePath = Path.GetFullPath(absolutePath);

								if (!File.Exists(absolutePath))
								{
									isValid = false;
									await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Couldn't locate file '{absolutePath}' in template '{entries[i].Name}'. Skipping.");
									break;
								}

								entries[i].AbsolutePaths.Add(absolutePath);
							}

							if (!isValid)
							{
								entries.RemoveAt(i);
							}
						}

						await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Found {entries.Count} valid template entries");
					}

					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Finished loading config.");
					await EstablishPerforceConnectionAsync();
				}
				else
				{
					Directory.CreateDirectory(Path.GetDirectoryName(filePath));
					File.Create(filePath).Dispose();
				}
			}
			catch (Exception ex)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Exception while loading File Template Creator Config File",
					ex);
			}
		}

		public async Task<bool> SaveConfigAsync()
		{
			var options = new JsonSerializerOptions { WriteIndented = true };
			string configFilePath = GetConfigFilePath();

			await EstablishPerforceConnectionAsync();
			bool isWritable = configFilePath.IsFileWritable();

			if (isWritable)
			{
				using FileStream fileStream = File.Create(configFilePath);
				JsonSerializer.Serialize(fileStream, CreatorConfig, options);
			}

			return isWritable;
		}

		public async Task<bool> EstablishPerforceConnectionAsync()
		{
			if (!string.IsNullOrWhiteSpace(CreatorConfig.P4Server))
			{
				PerforceID id = new PerforceID(CreatorConfig.P4Server, CreatorConfig.P4UserName, CreatorConfig.P4Workspace);
				await PerforceConnection.InitAsync(id);
			}

			return false;
		}

		public CFileTemplateCreatorConfig CreatorConfig { get; set; }
		private FileSystemWatcher ConfigFileWatcher { get; set; }

		private string SolutionFolder { get; set; } = "";
	}
}
