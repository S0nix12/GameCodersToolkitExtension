using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace DataReferenceFinder.Configuration
{
	public class CDataLocationEntry
	{
		public string Name { get; set; }
		public string Path { get; set; }
		public List<string> ExtensionFilters { get; set; } = new List<string>();
	}

	public class CDataLocationsConfig
	{
		public List<CDataLocationEntry> DataLocationEntries { get; set; } = new List<CDataLocationEntry>();
	}

	public class CDataLocationsConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "DataReferenceFinder/DataReferenceFinderConfig.json";

		public async Task InitAsync()
		{
			bool isSolutionOpen = await VS.Solutions.IsOpenAsync();

			if (isSolutionOpen)
			{
				HandleOpenSolution();
			}

			VS.Events.SolutionEvents.OnAfterOpenSolution += HandleOpenSolution;
		}

		public List<CDataLocationEntry> GetLocationEntries()
		{
			return DataLocationsConfig.DataLocationEntries;
		}

		private void HandleOpenSolution(Solution? solution = null)
		{
			ThreadHelper.JoinableTaskFactory.Run(LoadSolutionConfigAsync);
		}

		public async Task LoadSolutionConfigAsync()
		{
			string configFilePath = await GetConfigFilePathAsync();

			ConfigFileWatcher?.Dispose();
			ConfigFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(configFilePath));
			ConfigFileWatcher.EnableRaisingEvents = true;
			ConfigFileWatcher.Changed += OnConfigFileChanged;
			ConfigFileWatcher.IncludeSubdirectories = false;
			ConfigFileWatcher.Filter = "*.json";
			ConfigFileWatcher.NotifyFilter = NotifyFilters.LastWrite;

			await LoadConfigAsync(configFilePath);
		}

		private void OnConfigFileChanged(object sender, FileSystemEventArgs eventArgs)
		{
			ThreadHelper.JoinableTaskFactory.Run(async delegate { await LoadConfigAsync(eventArgs.FullPath); });
		}

		private async Task<string> GetConfigFilePathAsync()
		{
			var solution = await VS.Solutions.GetCurrentSolutionAsync();
			string configDir = Path.GetDirectoryName(solution.FullPath);
			string configFilePath = Path.Combine(configDir, cConfigFilePath);
			return configFilePath;
		}

		private async Task LoadConfigAsync(string filePath)
		{
			try
			{
				FileOptions combinedOption = FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.None;
				using var fileStream = new FileStream(
					filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, combinedOption);

				DataLocationsConfig = await JsonSerializer.DeserializeAsync<CDataLocationsConfig>(fileStream);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine(ex.Message);
				System.Diagnostics.Debug.WriteLine(ex.StackTrace);
				await DataReferenceFinderPackage.ExtensionOutput.WriteLineAsync(ex.Message);
				await DataReferenceFinderPackage.ExtensionOutput.WriteLineAsync(ex.StackTrace);
			}
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

			var options = new JsonSerializerOptions { WriteIndented = true };
			string configFilePath = await GetConfigFilePathAsync();
			
			using FileStream fileStream = File.Create(configFilePath);
			await JsonSerializer.SerializeAsync(fileStream, DataLocationsConfig, options);
		}

		private CDataLocationsConfig DataLocationsConfig { get; set; } = new CDataLocationsConfig();
		private FileSystemWatcher ConfigFileWatcher { get; set; }
	}
}
