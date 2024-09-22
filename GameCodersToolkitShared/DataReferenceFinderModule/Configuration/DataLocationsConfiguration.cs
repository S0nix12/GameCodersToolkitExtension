using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.Utils;

namespace GameCodersToolkit.Configuration
{
	public class CDataLocationEntry
	{
		public string Name { get; set; }
		public string Path { get; set; }
		public List<string> ExtensionFilters { get; set; } = new List<string>();
		public List<string> UsedParsingDescriptions { get; set; } = new List<string>();
	}

	public class CDataLocationsConfig
	{
		public List<CDataLocationEntry> DataLocationEntries { get; set; } = new List<CDataLocationEntry>();
		public List<DataParsingDescription> DataParsingDescriptions { get; set; } = new List<DataParsingDescription>();
		public List<string> GuidFieldIdentifiers { get; set; } = new List<string>();
		public string DataEditorServerUri { get; set; } = "";
	}

	public class CDataLocationsConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "DataReferenceFinder/DataReferenceFinderConfig.json";

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

		public List<CDataLocationEntry> GetLocationEntries()
		{
			return DataLocationsConfig.DataLocationEntries;
		}

		public List<DataParsingDescription> GetParsingDescriptions()
		{
			return DataLocationsConfig.DataParsingDescriptions;
		}

		public List<string> GetGuidFieldIdentifiers()
		{
			return DataLocationsConfig.GuidFieldIdentifiers;
		}

		public string GetDataEditorServerUri()
		{
			return DataLocationsConfig.DataEditorServerUri;
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

			ConfigFileWatcher?.Dispose();
			ConfigFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(configFilePath));
			ConfigFileWatcher.EnableRaisingEvents = true;
			ConfigFileWatcher.Changed += OnConfigFileChanged;
			ConfigFileWatcher.IncludeSubdirectories = false;
			ConfigFileWatcher.Filter = "*.*";
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

		private async Task LoadConfigAsync(string filePath)
		{
			try
			{
				if (File.Exists(filePath))
				{
					FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
					using var fileStream = new FileStream(
						filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

					DataLocationsConfig = await JsonSerializer.DeserializeAsync<CDataLocationsConfig>(fileStream);
					List<CDataLocationEntry> entries = DataLocationsConfig.DataLocationEntries;
					foreach (CDataLocationEntry entry in entries)
					{
						if (!Path.IsPathRooted(entry.Path))
						{
							lock (SolutionFolder)
							{
								entry.Path = Path.Combine(SolutionFolder, entry.Path);
							}
						}
						entry.Path = Path.GetFullPath(entry.Path);
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
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Exception while loading Data Reference Finder Config File",
					ex);
			}
			ConfigLoaded?.Invoke(this, new EventArgs());
		}

		public async Task SaveConfigAsync()
		{
			// Add mock data
			DataLocationsConfig.DataLocationEntries.Clear();

			CDataLocationEntry sampleConfigEntry = new CDataLocationEntry();
			sampleConfigEntry.Path = "E:\\KlaxEngineProject_MockData";
			sampleConfigEntry.Name = "Full Mock Data";
			sampleConfigEntry.ExtensionFilters.Add("*");
			sampleConfigEntry.ExtensionFilters.Add(".json");
			sampleConfigEntry.ExtensionFilters.Add(".xml");

			DataLocationsConfig.DataLocationEntries.Add(sampleConfigEntry);

			CDataLocationEntry hddConfigEntry = new CDataLocationEntry();
			hddConfigEntry.Path = "D:\\Documents\\KlaxEngineProject_MockData";
			hddConfigEntry.Name = "Full HDD Mock Data";

			DataLocationsConfig.DataLocationEntries.Add(hddConfigEntry);

			CDataLocationEntry smallDataConfigEntry = new CDataLocationEntry();
			smallDataConfigEntry.Path = "E:\\KlaxEngineProject_MockData\\ProjectData_1";
			smallDataConfigEntry.Name = "Small Mock Data Subset";
			smallDataConfigEntry.ExtensionFilters.Add(".json");

			DataLocationsConfig.DataLocationEntries.Add(smallDataConfigEntry);
			DataLocationsConfig.GuidFieldIdentifiers.Add("ExampleIdentifier");

			var options = new JsonSerializerOptions { WriteIndented = true };
			string configFilePath = GetConfigFilePath();

			using FileStream fileStream = File.Create(configFilePath);
			await JsonSerializer.SerializeAsync(fileStream, DataLocationsConfig, options);
		}

		public EventHandler ConfigLoaded { get; set; }
		private CDataLocationsConfig DataLocationsConfig { get; set; } = new CDataLocationsConfig();
		private FileSystemWatcher ConfigFileWatcher { get; set; }
		private string SolutionFolder { get; set; } = "";
	}
}
