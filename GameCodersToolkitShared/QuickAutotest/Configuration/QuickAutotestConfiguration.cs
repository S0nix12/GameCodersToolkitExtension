using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using GameCodersToolkitShared.Utils;

namespace GameCodersToolkit.Configuration
{
	public class QuickAutotestDirectoryEntry
	{
		public string DirectoryPath { get; set; }
		public string FilePattern { get; set; } = "*.bat";
		public bool IncludeSubfolders { get; set; } = false;
		public Dictionary<string, int> HighPriorityFiles { get; set; } = [];

		[JsonIgnore]
		public string DirectoryPathAbsolute { get; set; }
	}

	public class QuickAutotestConfig : BaseConfig
	{
		public List<QuickAutotestDirectoryEntry> AutotestDirectories { get; set; } = [];
	}
	public class QuickAutotestEntry
	{
		public string GetFormatedEntryName()
		{
			if (string.IsNullOrEmpty(Name))
			{
				return $"Path: {FilePath}";
			}
			else
			{
				return Name;
			}
		}

		public string FilePath { get; set; }
		public string Name { get; set; }
	}

	public class CQuickAutotestConfiguration : ModuleBaseConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "QuickAutotest/QuickAutotestConfig.json";

		public QuickAutotestConfig Config { get { return GetConfig<QuickAutotestConfig>(); } }

		public CQuickAutotestConfiguration()
		{
			ModuleName = "QuickAutotest";

			AddConfigFile<QuickAutotestConfig>("Config", cConfigFilePath);
		}

		protected override BaseConfig GetNewConfigObject(string name, Type type)
		{
			QuickAutotestConfig config = new QuickAutotestConfig();
			config.AutotestDirectories = new List<QuickAutotestDirectoryEntry>()
			{
				new QuickAutotestDirectoryEntry()
				{
					DirectoryPath = "OuterFolder/InnerFolder/AnotherFolder/",
					FilePattern = "*.bat",
					IncludeSubfolders = true,
					HighPriorityFiles = new Dictionary<string, int>() 
					{
						{ "DoNothing.bat", 2 }
					}
				}
			};

			return config;
		}

		protected override async Task PostConfigLoad(string name, Type type, object configObject)
		{
			QuickAutotestService service = await GameCodersToolkitPackage.Package.GetServiceAsync<QuickAutotestService, QuickAutotestService>();

			service.Autotests.Clear();

			// Order autotest files
			{
				List<Tuple<int, QuickAutotestEntry>> sortingPairs = new List<Tuple<int, QuickAutotestEntry>>();

				foreach (var entry in Config.AutotestDirectories)
				{
					if (!string.IsNullOrWhiteSpace(entry.DirectoryPath))
					{
						lock (SolutionFolder)
						{
							if (System.IO.Path.IsPathRooted(entry.DirectoryPath))
							{
								entry.DirectoryPathAbsolute = entry.DirectoryPath;
							}
							else
							{
								entry.DirectoryPathAbsolute = Path.Combine(SolutionFolder, "QuickAutotest/", entry.DirectoryPath);
							}
						}
					}

					if (!Directory.Exists(entry.DirectoryPathAbsolute))
					{
						continue;
					}

					string[] files = Directory.GetFiles(entry.DirectoryPathAbsolute, entry.FilePattern, entry.IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);

					foreach (string file in files)
					{
						QuickAutotestEntry newEntry = new QuickAutotestEntry();
						newEntry.FilePath = file;
						newEntry.Name = Path.GetFileNameWithoutExtension(file);

						if (entry.HighPriorityFiles.TryGetValue(Path.GetFileName(file), out int priority))
						{
							sortingPairs.Add(new Tuple<int, QuickAutotestEntry>(priority, newEntry));
						}
						else
						{
							sortingPairs.Add(new Tuple<int, QuickAutotestEntry>(0, newEntry));
						}
					}
				}

				var sortedFiles = sortingPairs.OrderBy(x => x.Item1).ThenBy(x => x.Item2.Name).ToList();

				foreach (var pair in sortedFiles)
				{
					service.Autotests.Add(pair.Item2);
				}
			}
		}
	}
}
