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


		[JsonIgnore]
		public string ParserConfigPathAbsolute { get; set; }
		[JsonIgnore]
		public JsonDocument ParserConfig { get; set; }
	}

	public class CFileTemplateCreatorUserConfig
    {
        public string PostChangeScriptPath { get; set; }
        [JsonIgnore]
        public string PostChangeScriptAbsolutePath { get; set; }


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
    }


    public class CFileTemplateCreatorConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "FileTemplateCreator/FileTemplateCreatorConfig.json";
        public const string cUserConfigFilePath = "FileTemplateCreator/FileTemplateCreatorUserConfig.json";

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
			string[] configFilePaths = GetConfigFilePaths();
            await LoadConfigsAsync(configFilePaths[0], configFilePaths[1]);

			string configDirectory = Path.GetDirectoryName(configFilePaths[0]);
            string userConfigDirectory = Path.GetDirectoryName(configFilePaths[1]);
            bool hasDifferentPaths = configDirectory != userConfigDirectory;

            ConfigFileWatcher?.Dispose();
			ConfigFileWatcher = new FileSystemWatcher(configDirectory)
			{
				EnableRaisingEvents = true
			};
			ConfigFileWatcher.Changed += OnConfigFileChanged;
			ConfigFileWatcher.IncludeSubdirectories = false;
			ConfigFileWatcher.Filter = "*.json";
			ConfigFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastAccess;

			if (hasDifferentPaths)
            {
                ConfigFileWatcher?.Dispose();
                ConfigFileWatcher = new FileSystemWatcher(userConfigDirectory)
                {
                    EnableRaisingEvents = true
                };
                ConfigFileWatcher.Changed += OnConfigFileChanged;
                ConfigFileWatcher.IncludeSubdirectories = false;
                ConfigFileWatcher.Filter = "*.json";
                ConfigFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastAccess;
            }
        }

		private void OnConfigFileChanged(object sender, FileSystemEventArgs eventArgs)
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate 
			{
                string[] configFilePaths = GetConfigFilePaths();
                await LoadConfigsAsync(configFilePaths[0], configFilePaths[1]);
            });
		}

		public string[] GetConfigFilePaths()
		{
			lock (SolutionFolder)
			{
                string[] paths = [Path.Combine(SolutionFolder, cConfigFilePath), Path.Combine(SolutionFolder, cUserConfigFilePath)];
				return paths;
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
			if (File.Exists(UserConfig.PostChangeScriptAbsolutePath))
			{
				System.Diagnostics.Process.Start(UserConfig.PostChangeScriptAbsolutePath);
            }

            await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                DTE2 dte = GameCodersToolkitPackage.Package.GetService<EnvDTE.DTE, DTE2>();
                if (dte != null)
				{
                    bool isBuildingAlready = dte.Solution.SolutionBuild.BuildState == EnvDTE.vsBuildState.vsBuildStateInProgress;

					if (!isBuildingAlready && !string.IsNullOrWhiteSpace(UserConfig.PostChangeProjectToBuild))
					{
					    var project = await VS.Solutions.FindProjectsAsync(UserConfig.PostChangeProjectToBuild);
					    if (project != null)
					    {
					        project.BuildAsync(BuildAction.Build);
					    }
					}
				}
            });
		}

		private async Task LoadConfigsAsync(string creatorConfigFilePath, string userConfigFilePath)
		{
			try
			{
				if (CreatorConfig != null || UserConfig != null)
				{
					await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Reloading FileTemplateCreator config after change");
				}
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Attempting to load FileTemplateCreator config at '{creatorConfigFilePath}' & user config at {userConfigFilePath}");

				if (File.Exists(creatorConfigFilePath))
				{
					FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
					using var fileStream = new FileStream(
                        creatorConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

					CreatorConfig = await JsonSerializer.DeserializeAsync<CFileTemplateCreatorConfig>(fileStream);

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
					Directory.CreateDirectory(Path.GetDirectoryName(creatorConfigFilePath));
					File.Create(creatorConfigFilePath).Dispose();
				}

                if (File.Exists(userConfigFilePath))
                {
                    FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
                    using var fileStream = new FileStream(
                        userConfigFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

                    UserConfig = await JsonSerializer.DeserializeAsync<CFileTemplateCreatorUserConfig>(fileStream);

                    // Post-Change Script
                    {
                        if (!string.IsNullOrWhiteSpace(UserConfig.PostChangeScriptPath) && !Path.IsPathRooted(UserConfig.PostChangeScriptPath))
                        {
                            lock (SolutionFolder)
                            {
                                UserConfig.PostChangeScriptAbsolutePath = Path.Combine(SolutionFolder, UserConfig.PostChangeScriptPath);
                            }

                            UserConfig.PostChangeScriptAbsolutePath = Path.GetFullPath(UserConfig.PostChangeScriptAbsolutePath);
                        }

                        bool exists = File.Exists(UserConfig.PostChangeScriptAbsolutePath);
                        await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] PostChangeScript at '{UserConfig.PostChangeScriptAbsolutePath}' (File exists: {exists})");
                    }

                    // Post-Change project to build
                    {
                        bool exists = (await VS.Solutions.FindProjectsAsync(UserConfig.PostChangeProjectToBuild)) != null;
                        await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] PostChangeProjectToBuild is '{UserConfig.PostChangeProjectToBuild}' (Exists: {exists})");
                    }

                    await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync($"[FileTemplateCreator] Finished loading user config.");
                    await EstablishPerforceConnectionAsync();
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(userConfigFilePath));
                    File.Create(userConfigFilePath).Dispose();
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
			string[] configFilePaths = GetConfigFilePaths();

			await EstablishPerforceConnectionAsync();
			bool isWritable = configFilePaths[1].IsFileWritable();

			if (isWritable)
			{
				using FileStream fileStream = File.Create(configFilePaths[1]);
				JsonSerializer.Serialize(fileStream, CreatorConfig, options);
			}

			return isWritable;
		}

		public async Task<bool> EstablishPerforceConnectionAsync()
		{
			if (!string.IsNullOrWhiteSpace(UserConfig.P4Server))
			{
				PerforceID id = new PerforceID(UserConfig.P4Server, UserConfig.P4UserName, UserConfig.P4Workspace);
				await PerforceConnection.InitAsync(id);
			}

			return false;
		}

		public CFileTemplateCreatorConfig CreatorConfig { get; set; } = new CFileTemplateCreatorConfig();
		public CFileTemplateCreatorUserConfig UserConfig { get; set; } = new CFileTemplateCreatorUserConfig();
		private FileSystemWatcher ConfigFileWatcher { get; set; }

		private string SolutionFolder { get; set; } = "";
	}
}
