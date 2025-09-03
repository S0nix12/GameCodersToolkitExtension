using System.Text.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.Utils;
using Microsoft.VisualStudio.Threading;
using GameCodersToolkitShared.Utils;

namespace GameCodersToolkit.Configuration
{
	public class CDataLocationEntry
	{
		public string Name { get; set; }
		public string Path { get; set; }
		public List<string> ExtensionFilters { get; set; } = new List<string>();
		public List<string> UsedParsingDescriptions { get; set; } = new List<string>();
	}

	public class CDataLocationsConfig : BaseConfig
	{
		public string DataProjectBasePath { get; set; } = "";
		public List<CDataLocationEntry> DataLocationEntries { get; set; } = new List<CDataLocationEntry>();
		public List<DataParsingDescription> DataParsingDescriptions { get; set; } = new List<DataParsingDescription>();
		public List<string> GuidFieldIdentifiers { get; set; } = new List<string>();
		public string DataEditorServerUri { get; set; } = "";
	}

	public class CDataLocationsConfiguration : ModuleBaseConfiguration
	{
		// Relative to the Solution Directory
		public const string cConfigFilePath = "DataReferenceFinder/DataReferenceFinderConfig.json";

		public CDataLocationsConfiguration()
		{
			ModuleName = "DataReferenceFinder";

			AddConfigFile<CDataLocationsConfig>("DataLocationsConfig", cConfigFilePath);
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

		public string GetDataProjectBasePath()
		{
			return DataLocationsConfig.DataProjectBasePath;
		}

		public string GetConfigFilePath()
		{
			return GetConfigFilePath<CDataLocationsConfig>();
		}

		protected override async Task PostConfigLoad(string name, Type type, object configObject)
		{
			if (type == typeof(CDataLocationsConfig))
			{
				List<CDataLocationEntry> entries = DataLocationsConfig.DataLocationEntries;
				if (!string.IsNullOrEmpty(DataLocationsConfig.DataProjectBasePath))
				{
					if (!Path.IsPathRooted(DataLocationsConfig.DataProjectBasePath))
					{
						lock (SolutionFolder)
						{
							DataLocationsConfig.DataProjectBasePath = Path.Combine(SolutionFolder, DataLocationsConfig.DataProjectBasePath);
						}
					}
					DataLocationsConfig.DataProjectBasePath = Path.GetFullPath(DataLocationsConfig.DataProjectBasePath);
				}

				foreach (CDataLocationEntry entry in entries)
				{
					if (!Path.IsPathRooted(entry.Path))
					{
						if (Directory.Exists(DataLocationsConfig.DataProjectBasePath))
						{
							entry.Path = Path.Combine(DataLocationsConfig.DataProjectBasePath, entry.Path);
						}
						else
						{
							lock (SolutionFolder)
							{
								entry.Path = Path.Combine(SolutionFolder, entry.Path);
							}
						}
					}
					entry.Path = Path.GetFullPath(entry.Path);
				}
				await GameCodersToolkitPackage.ExtensionOutput.WriteLineAsync("[DataReferenceFinder] Parsed new DataLocationsConfig");
			}
			await ConfigLoaded?.InvokeAsync(this, new EventArgs());
		}

		public AsyncEventHandler ConfigLoaded { get; set; }
		private CDataLocationsConfig DataLocationsConfig { get { return GetConfig<CDataLocationsConfig>(); } }
	}
}
