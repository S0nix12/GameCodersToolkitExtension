using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using System.Linq;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using GameCodersToolkit.SourceControl;
using System.IO.Pipes;
using GameCodersToolkit.Utils;
using System.Diagnostics;

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
		public List<string> Paths { get; set; } = new List<string>();

		[JsonIgnore]
		public List<string> AbsolutePaths { get; set; } = new List<string>();
	}

	public class CFileTemplateCreatorConfig
	{
		public List<CMakeFileEntry> CMakeFileEntries { get; set; } = new List<CMakeFileEntry>();
		public List<CTemplateEntry> FileTemplateEntries { get; set; } = new List<CTemplateEntry>();

		public string ParserName { get; set; }
		public string ParserConfigPath { get; set; }
		[JsonIgnore]
		public string ParserConfigString { get; set; }

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
				HandleOpenSolution(await VS.Solutions.GetCurrentSolutionAsync());
			}

			VS.Events.SolutionEvents.OnAfterOpenSolution += HandleOpenSolution;
		}

		private void HandleOpenSolution(Solution solution = null)
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
			return CreatorConfig.CMakeFileEntries.Where(makeFile => makeFile.ID == id).FirstOrDefault()?.AbsolutePath;
		}

		public CTemplateEntry GetTemplateByName(string name)
		{
			return CreatorConfig.FileTemplateEntries.Where(entry => entry.Name == name).FirstOrDefault();
		}

		public Type GetParserConfigAs<Type>()
		{
			return JsonSerializer.Deserialize<Type>(CreatorConfig.ParserConfigString);
		}

		public void ExecutePostBuildScript()
		{
			if (File.Exists(CreatorConfig.PostChangeScriptAbsolutePath))
			{
                Process.Start(CreatorConfig.PostChangeScriptAbsolutePath);
            }
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
						if (!string.IsNullOrWhiteSpace(CreatorConfig.PostChangeScriptPath) && !Path.IsPathRooted(CreatorConfig.PostChangeScriptPath))
						{
							lock (SolutionFolder)
							{
								CreatorConfig.PostChangeScriptAbsolutePath = Path.Combine(SolutionFolder, CreatorConfig.PostChangeScriptPath);
							}
						}
					}

					// Parser config path
					{
						if (!string.IsNullOrWhiteSpace(CreatorConfig.ParserConfigPath))
						{
							lock (SolutionFolder)
							{
								CreatorConfig.ParserConfigPath = Path.Combine(SolutionFolder, CreatorConfig.ParserConfigPath);
							}

							if (File.Exists(CreatorConfig.ParserConfigPath))
							{
								CreatorConfig.ParserConfigString = File.ReadAllText(CreatorConfig.ParserConfigPath);
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
									cmakeFileEntry.AbsolutePath = Path.Combine(SolutionFolder, cmakeFileEntry.Path);
								}
							}
							cmakeFileEntry.AbsolutePath = Path.GetFullPath(cmakeFileEntry.Path);
						}
					}

					// Templates
					{
						List<CTemplateEntry> entries = CreatorConfig.FileTemplateEntries;

						foreach (CTemplateEntry entry in entries)
						{
							entry.AbsolutePaths = Enumerable.Repeat(string.Empty, entry.Paths.Count).ToList();
							for (int i = entry.Paths.Count - 1; i >= 0; i--)
							{
								if (!Path.IsPathRooted(entry.Paths[i]))
								{
									lock (SolutionFolder)
									{
										entry.AbsolutePaths[i] = Path.Combine(SolutionFolder, entry.Paths[i]);
									}
								}
								entry.AbsolutePaths[i] = Path.GetFullPath(entry.Paths[i]);

								if (!File.Exists(entry.AbsolutePaths[i]))
								{
									entry.AbsolutePaths.RemoveAt(i);
								}
							}
						}
					}

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

		public CFileTemplateCreatorConfig CreatorConfig { get; set; } = new CFileTemplateCreatorConfig();
		private FileSystemWatcher ConfigFileWatcher { get; set; }

		private string SolutionFolder { get; set; } = "";
	}
}
