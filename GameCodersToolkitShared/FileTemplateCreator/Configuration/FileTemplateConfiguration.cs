using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameCodersToolkit.FileTemplateCreator.MakeFileParser;
using System.Linq;
using System.Text.Json.Serialization;
using GameCodersToolkit.Utils;
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


    public class CFileTemplateCreatorConfiguration : ModuleBaseConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "FileTemplateCreator/FileTemplateCreatorConfig.json";

		public CFileTemplateCreatorConfig CreatorConfig { get { return GetConfig<CFileTemplateCreatorConfig>(); } }

		public CFileTemplateCreatorConfiguration()
		{
			ModuleName = "FileTemplateCreator";

			AddConfigFile<CFileTemplateCreatorConfig>("MainConfig", cConfigFilePath);
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
			var sharedConfig = GameCodersToolkitPackage.SharedConfig;
			if (sharedConfig != null)
			{
				await sharedConfig.ExecutePostBuildStepsAsync();
			}
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
		}

		public async Task<bool> EstablishPerforceConnectionAsync()
		{
			var sharedConfig = GameCodersToolkitPackage.SharedConfig;
			if (sharedConfig != null)
			{
				return await sharedConfig.EstablishPerforceConnectionAsync();
			}

			return false;
		}
	}
}
