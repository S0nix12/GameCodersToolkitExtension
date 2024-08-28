using System.IO;

namespace DataReferenceFinder
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.OpenConfigFile)]
	internal sealed class OpenConfigFileCommand : BaseCommand<OpenConfigFileCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			string configFilePath = DataReferenceFinderPackage.DataLocationsConfig.GetConfigFilePath();
			configFilePath = Path.GetFullPath(configFilePath);
			if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
			{
				await VS.Documents.OpenAsync(configFilePath);
			}
		}
	}
}
