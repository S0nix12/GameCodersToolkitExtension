using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using System.Linq;

namespace GameCodersToolkit.Configuration
{
	public class CMakeFileEntry
	{
		public string ID { get; set; }
		public string Path { get; set; }
	}

	public class CTemplateEntry
	{
		public string Name { get; set; }
		public string MakeFileID { get; set; }
		public List<string> Paths { get; set; } = new List<string>();
	}

	public class CFileTemplateCreatorConfig
	{
		public List<CMakeFileEntry> CMakeFileEntries { get; set; } = new List<CMakeFileEntry>();
		public List<CTemplateEntry> FileTemplateEntries { get; set; } = new List<CTemplateEntry>();
		public string ParserName { get; set; }
		public string PostChangeScriptPath { get; set; }
	}

	public class CFileTemplateConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "FileTemplateCreator/FileTemplateCreatorConfig.json";

		public async Task InitAsync()
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			bool isSolutionOpen = await VS.Solutions.IsOpenAsync();

			if (isSolutionOpen)
			{
				HandleOpenSolution();
			}

			VS.Events.SolutionEvents.OnAfterOpenSolution += HandleOpenSolution;
		}

		private void HandleOpenSolution(Solution? solution = null)
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

			//ConfigFileWatcher?.Dispose();
			//ConfigFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(configFilePath));
			//ConfigFileWatcher.EnableRaisingEvents = true;
			//ConfigFileWatcher.Changed += OnConfigFileChanged;
			//ConfigFileWatcher.IncludeSubdirectories = false;
			//ConfigFileWatcher.Filter = "*.json";
			//ConfigFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.LastAccess;
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

		public string GetMakeFilePathByID(string id)
		{
			return CreatorConfig.CMakeFileEntries.Where(makeFile => makeFile.ID == id).FirstOrDefault()?.Path;
		}

		private async Task LoadConfigAsync(string filePath)
		{
			try
			{
				if (File.Exists(filePath))
				{
					FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
					using var fileStream = new FileStream(
						filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

					CreatorConfig = await JsonSerializer.DeserializeAsync<CFileTemplateCreatorConfig>(fileStream);

					// Post-Change Script
					{
						if (!Path.IsPathRooted(CreatorConfig.PostChangeScriptPath))
						{
							lock (SolutionFolder)
							{
								CreatorConfig.PostChangeScriptPath = Path.Combine(SolutionFolder, CreatorConfig.PostChangeScriptPath);
							}
						}
					}

					// Makefiles
					{
						List<CMakeFileEntry> entries = CreatorConfig.CMakeFileEntries;

						foreach (CMakeFileEntry cmakeFileEntry in entries)
						{
							if (!Path.IsPathRooted(cmakeFileEntry.Path))
							{
								lock (SolutionFolder)
								{
									cmakeFileEntry.Path = Path.Combine(SolutionFolder, cmakeFileEntry.Path);
								}
							}
							cmakeFileEntry.Path = Path.GetFullPath(cmakeFileEntry.Path);
						}
					}

					// Templates
					{
						List<CTemplateEntry> entries = CreatorConfig.FileTemplateEntries;

						foreach (CTemplateEntry entry in entries)
						{
							for (int i = entry.Paths.Count - 1; i >= 0; i--)
							{
								if (!Path.IsPathRooted(entry.Paths[i]))
								{
									lock (SolutionFolder)
									{
										entry.Paths[i] = Path.Combine(SolutionFolder, entry.Paths[i]);
									}
								}
								entry.Paths[i] = Path.GetFullPath(entry.Paths[i]);

								if (!File.Exists(entry.Paths[i]))
								{
									entry.Paths.RemoveAt(i);
								}
							}
						}
					}
				}
				else
				{
					Directory.CreateDirectory(Path.GetDirectoryName(filePath));
					File.Create(filePath).Dispose();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
				System.Diagnostics.Debug.WriteLine(ex.StackTrace);
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync(ex.Message);
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync(ex.StackTrace);
			}
		}

		public async Task SaveConfigAsync()
		{
			// Add mock data
			CreatorConfig.CMakeFileEntries.Clear();
			CreatorConfig.FileTemplateEntries.Clear();

			CMakeFileEntry sampleConfigEntry = new CMakeFileEntry();
			sampleConfigEntry.Path = "FileTemplateCreator\\ExampleList.cmake";
			sampleConfigEntry.ID = "Default";

			CreatorConfig.CMakeFileEntries.Add(sampleConfigEntry);

			CTemplateEntry sampleTemplateEntry = new CTemplateEntry();
			sampleTemplateEntry.Name = "Schematyc Component - Entity";
			sampleTemplateEntry.MakeFileID = "Default";
			sampleTemplateEntry.Paths.Add("FileTemplateCreator\\Templates\\SchematycEntityComponent.h");
			sampleTemplateEntry.Paths.Add("FileTemplateCreator\\Templates\\SchematycEntityComponent.cpp");

			CreatorConfig.FileTemplateEntries.Add(sampleTemplateEntry);

			CreatorConfig.ParserName = "GameCodersToolkit.FileTemplateCreator.MakeFileParser.CryGameParser";

			var options = new JsonSerializerOptions { WriteIndented = true };
			string configFilePath = GetConfigFilePath();

			using FileStream fileStream = File.Create(configFilePath);
			await JsonSerializer.SerializeAsync(fileStream, CreatorConfig, options);
		}

		public CFileTemplateCreatorConfig CreatorConfig { get; set; } = new CFileTemplateCreatorConfig();
		private FileSystemWatcher ConfigFileWatcher { get; set; }

		private string SolutionFolder { get; set; } = "";
	}
}
