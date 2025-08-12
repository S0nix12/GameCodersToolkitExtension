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
using GameCodersToolkitShared.Utils;

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

	public class CFileTemplateCreatorConfig : BaseConfig
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

	public class CFileTemplateCreatorUserConfig : BaseConfig
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


    public class CFileTemplateCreatorConfiguration : ModuleBaseConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "FileTemplateCreator/FileTemplateCreatorConfig.json";
        public const string cUserConfigFilePath = "FileTemplateCreator/FileTemplateCreatorUserConfig.json";

		public CFileTemplateCreatorConfig CreatorConfig { get { return GetConfig<CFileTemplateCreatorConfig>(); } }
		public CFileTemplateCreatorUserConfig UserConfig { get { return GetConfig<CFileTemplateCreatorUserConfig>(); } }

		public CFileTemplateCreatorConfiguration()
		{
			ModuleName = "FileTemplateCreator";

			AddConfigFile<CFileTemplateCreatorConfig>("MainConfig", cConfigFilePath);
			AddConfigFile<CFileTemplateCreatorUserConfig>("UserConfig", cUserConfigFilePath);

			OnConfigLoadSucceeded += (s, e) => EstablishPerforceConnectionAsync();
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

		protected override async Task PostConfigLoad(string name, Type type, object configObject)
		{
			if (type == typeof(CFileTemplateCreatorConfig))
			{
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
			}
			else if (type == typeof(CFileTemplateCreatorUserConfig))
			{
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
			}
		}

		public async Task<bool> EstablishPerforceConnectionAsync()
		{
			if (false && !string.IsNullOrWhiteSpace(UserConfig.P4Server))
			{
				PerforceID id = new PerforceID(UserConfig.P4Server, UserConfig.P4UserName, UserConfig.P4Workspace);
				await PerforceConnection.InitAsync(id);
			}

			return false;
		}
	}
}
